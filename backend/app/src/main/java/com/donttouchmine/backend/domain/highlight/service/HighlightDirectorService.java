package com.donttouchmine.backend.domain.highlight.service;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.ObjectMapper;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Semaphore;
import java.util.concurrent.TimeUnit;

import com.donttouchmine.backend.domain.highlight.dto.HighlightCandidate;
import com.donttouchmine.backend.domain.highlight.dto.HighlightDirectorReply;
import com.donttouchmine.backend.domain.highlight.dto.HighlightDirectorReply.Pick;

/**
 * GMS(Gemini) 에 하이라이트 후보를 보내 두 개를 고르고 제목과 한 줄 해설을 받습니다.
 *
 * <p>어떤 실패도 {@link HighlightDirectorReply#unavailable()} 로 끝납니다. 하이라이트는 모델 없이도
 * 규칙 순위로 나오므로, 모델은 있으면 좋은 것이지 경기 종료를 붙잡을 이유가 아닙니다.
 */
@Service
public class HighlightDirectorService {

    private static final int MAX_PICKS = 2;
    private static final int MAX_CONCURRENT = 4;
    private static final Duration CONNECT_TIMEOUT = Duration.ofSeconds(1);
    private static final Duration CALL_TIMEOUT = Duration.ofMillis(2600);
    private static final int MAX_RESPONSE_CHARS = 32768;
    private static final int TITLE_LIMIT = 24;
    private static final int SUMMARY_LIMIT = 70;
    private static final Set<String> UNSAFE_PROBABILITIES = Set.of("MEDIUM", "HIGH");
    private static final List<String> BANNED_WORDS =
            List.of("시발", "씨발", "병신", "개새끼", "꺼져", "죽어", "fuck", "shit", "bitch");

    private static final String PROMPT = """
            숨바꼭질/물건 찾기 게임의 하이라이트 편집자다. 후보에서 재미와 사건의 명확성을 기준으로
            최대 2개(후보가 2개 미만이면 전부)를 순서대로 선택하라. 제공된 사건 외의 행동/승패/인물을
            지어내지 말 것. FirstBlood=첫 물건 파괴, TteTanMulgun=여러 사람이 만진 물건,
            FinalMoment=종료 직전 사건, LongestHidden=오래 숨겨진 물건, MostStunned=반복 기절,
            ItemDestroyed=물건 파괴, PlayerStunned=기절, ItemRecovered=원주인의 물건 회수.
            제목은 한국어 24자 이내, 요약은 한국어 한 줄 70자 이내. 욕설/비하/성적 표현/혐오/개인정보/
            마크업/URL을 사용하지 말 것. 재미있는 상황을 설명하되 플레이어를 조롱하지 말 것.
            오직 JSON {"picks":[{"id":0,"title":"제목","summary":"한 줄 해설"}]}로 반환.
            id는 입력 후보의 id만 사용하며 중복 없이 선택한다. 후보 JSON:
            """;

    private final String key;
    private final URI endpoint;
    private final ObjectMapper json;
    private final HttpClient http;

    /** 모델이 느려질 때 요청 스레드가 줄줄이 묶이지 않도록 동시 호출 수를 막습니다. */
    private final Semaphore capacity = new Semaphore(MAX_CONCURRENT);

    @Autowired
    public HighlightDirectorService(@Value("${GMS_KEY:}") String key,
                                    @Value("${GMS_GENERATE_URL:}") String url,
                                    ObjectMapper json) {
        this(key, url, json, HttpClient.newBuilder().connectTimeout(CONNECT_TIMEOUT).build());
    }

    HighlightDirectorService(String key, String url, ObjectMapper json, HttpClient http) {
        this.key = key;
        this.json = json;
        this.http = http;
        this.endpoint = safeEndpoint(url);
    }

    /** 키와 https 주소가 모두 있어야 켜집니다. */
    public boolean enabled() {
        return !key.isBlank() && endpoint != null;
    }

    public HighlightDirectorReply direct(List<HighlightCandidate> candidates) {
        if (!enabled() || !capacity.tryAcquire()) {
            return HighlightDirectorReply.unavailable();
        }

        CompletableFuture<HttpResponse<String>> pending = null;
        try {
            pending = http.sendAsync(request(candidates), HttpResponse.BodyHandlers.ofString());
            HttpResponse<String> response = pending.get(CALL_TIMEOUT.toMillis(), TimeUnit.MILLISECONDS);
            if (response.statusCode() != 200 || response.body().length() > MAX_RESPONSE_CHARS) {
                return HighlightDirectorReply.unavailable();
            }
            return parse(response.body(), candidates);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            return HighlightDirectorReply.unavailable();
        } catch (Exception ignored) {
            // Provider credentials, URL, response, and exceptions never enter client replies or logs.
            return HighlightDirectorReply.unavailable();
        } finally {
            if (pending != null && !pending.isDone()) {
                pending.cancel(true);
            }
            capacity.release();
        }
    }

    /**
     * 모델 응답을 읽습니다. 차단·잘림·형식 위반·후보 밖 id·위험한 문구 중 하나라도 있으면 통째로 버립니다.
     * 일부만 살리면 순위가 모델 것도 규칙 것도 아니게 됩니다.
     */
    HighlightDirectorReply parse(String body, List<HighlightCandidate> candidates) {
        try {
            JsonNode root = json.readTree(body);
            if (root.path("promptFeedback").has("blockReason")) {
                return HighlightDirectorReply.unavailable();
            }

            JsonNode answer = root.path("candidates").path(0);
            if (!"STOP".equals(answer.path("finishReason").asString())) {
                return HighlightDirectorReply.unavailable();
            }
            for (JsonNode rating : answer.path("safetyRatings")) {
                if (rating.path("blocked").asBoolean()
                        || UNSAFE_PROBABILITIES.contains(rating.path("probability").asString())) {
                    return HighlightDirectorReply.unavailable();
                }
            }

            JsonNode picks = json.readTree(answer.path("content").path("parts").path(0).path("text").asString())
                    .path("picks");
            if (!picks.isArray() || picks.size() != Math.min(MAX_PICKS, candidates.size())) {
                return HighlightDirectorReply.unavailable();
            }

            Set<Integer> valid = new HashSet<>();
            candidates.forEach(candidate -> valid.add(candidate.id()));
            Set<Integer> used = new HashSet<>();
            List<Pick> result = new ArrayList<>();

            for (JsonNode pick : picks) {
                if (!pick.path("id").isIntegralNumber()
                        || !pick.path("title").isString()
                        || !pick.path("summary").isString()) {
                    return HighlightDirectorReply.unavailable();
                }
                int id = pick.path("id").asInt();
                String title = pick.path("title").asString();
                String summary = pick.path("summary").asString();
                if (!valid.contains(id) || !used.add(id)
                        || !safe(title, TITLE_LIMIT) || !safe(summary, SUMMARY_LIMIT)) {
                    return HighlightDirectorReply.unavailable();
                }
                result.add(new Pick(id, title, summary));
            }
            return new HighlightDirectorReply(true, List.copyOf(result));
        } catch (Exception ignored) {
            return HighlightDirectorReply.unavailable();
        }
    }

    static boolean safe(String text, int limit) {
        if (text == null || text.isBlank() || text.length() > limit || !text.equals(text.strip())) {
            return false;
        }
        if (text.matches("(?s).*[<>\\p{Cntrl}].*") || text.contains("://") || text.contains("@")) {
            return false;
        }
        String compact = text.toLowerCase(Locale.ROOT).replaceAll("[\\s\\p{Punct}]", "");
        // Defense in depth, not a complete language classifier; blocked/uncertain provider output also falls back.
        return BANNED_WORDS.stream().noneMatch(compact::contains);
    }

    private HttpRequest request(List<HighlightCandidate> candidates) {
        String prompt = PROMPT + json.writeValueAsString(candidates);
        Map<String, Object> body = Map.of(
                "contents", List.of(Map.of("parts", List.of(Map.of("text", prompt)))),
                "generationConfig", Map.of(
                        "responseMimeType", "application/json",
                        "maxOutputTokens", 1200,
                        "thinkingConfig", Map.of("thinkingLevel", "minimal", "includeThoughts", false)));

        return HttpRequest.newBuilder(endpoint)
                .timeout(CALL_TIMEOUT)
                .header("Content-Type", "application/json")
                .header("x-goog-api-key", key)
                .POST(HttpRequest.BodyPublishers.ofString(json.writeValueAsString(body)))
                .build();
    }

    /** https 이고 호스트가 있고 사용자 정보가 없는 주소만 받습니다. 키가 엉뚱한 곳으로 새지 않게 합니다. */
    private static URI safeEndpoint(String url) {
        if (url == null || url.isBlank()) {
            return null;
        }
        try {
            URI parsed = URI.create(url);
            return "https".equals(parsed.getScheme()) && parsed.getHost() != null && parsed.getUserInfo() == null
                    ? parsed
                    : null;
        } catch (IllegalArgumentException e) {
            return null;
        }
    }
}
