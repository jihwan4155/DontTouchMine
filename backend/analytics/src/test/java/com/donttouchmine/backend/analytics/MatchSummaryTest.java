package com.donttouchmine.backend.analytics;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.RequestBuilder;
import tools.jackson.databind.ObjectMapper;

import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import static com.donttouchmine.backend.analytics.GameEventFixtures.fromIp;
import static com.donttouchmine.backend.analytics.GameEventFixtures.params;

import com.donttouchmine.backend.domain.analytics.dto.GameEventRequest;
import com.donttouchmine.backend.domain.analytics.repository.MatchSummaryWriter;
import com.donttouchmine.backend.domain.analytics.service.GameEventFlusher;
import com.donttouchmine.backend.support.AnalyticsIntegrationTest;

/**
 * 경기 요약 테이블 (V5). 요약은 뷰가 아니라 수집 서비스가 맞춰 두는 테이블이라, 원본 뷰와 같은 값을
 * 갖는지와 경기 하나를 맞추는 일이 정말 그 경기만 읽는지를 봅니다. 후자가 깨지면 1초마다 옛 뷰를 한 번씩
 * 부르는 것과 같아집니다.
 */
class MatchSummaryTest extends AnalyticsIntegrationTest {

    private static final String START = "{\"player_count\":2,\"hide_sec\":30,\"seek_sec\":300,\"stun_hits\":3,\"partial\":false}";

    @Autowired
    MockMvc mvc;

    @Autowired
    ObjectMapper objectMapper;

    @Autowired
    JdbcTemplate analytics;

    @Autowired
    MatchSummaryWriter summaries;

    @Autowired
    GameEventFlusher flusher;

    @Test
    @DisplayName("요약 테이블과 원본 뷰와 코드의 컬럼이 순서까지 같다")
    void columnsLineUp() {
        assertThat(columns("match_analysis_summary")).containsExactlyElementsOf(MatchSummaryWriter.COLUMNS);
        assertThat(columns("match_analysis_summary_source")).containsExactlyElementsOf(MatchSummaryWriter.COLUMNS);
    }

    @Test
    @DisplayName("수집 API 로 들어온 경기가 요약에 한 줄로 들어가고, 같은 이벤트를 다시 보내도 그대로다")
    void ingestedMatchAppearsOnce() throws Exception {
        String match = UUID.randomUUID().toString();
        String session = UUID.randomUUID().toString();
        long start = Instant.now().minusSeconds(120).toEpochMilli();
        List<GameEventRequest> events = List.of(
                hostEvent(session, 0, match, "match_start", start, 0, START),
                hostEvent(session, 1, match, "position_sample", start + 1_000, 1_000, "{\"seat\":0}"),
                hostEvent(session, 2, match, "match_end", start + 2_000, 2_000,
                        "{\"end_reason\":\"TimeUp\",\"expected_events\":3,\"dropped_samples\":0,\"partial\":false}"));

        mvc.perform(send("10.3.0.1", events)).andExpect(status().isAccepted());
        Map<String, Object> row = awaitSummary(match, 3);

        assertThat(row).as("플러셔가 5초 안에 세 건을 다 넣고 요약을 맞춰야 합니다").isNotNull();
        assertThat(((Number) row.get("player_count")).intValue()).isEqualTo(2);
        assertThat(((Number) row.get("upload_complete")).intValue()).isOne();

        // 스풀 재전송. 원본에서 걸러지므로 요약의 도착 건수도 그대로여야 합니다.
        mvc.perform(send("10.3.0.1", events)).andExpect(status().isAccepted());
        Thread.sleep(600);
        assertThat(((Number) summary(match).get("received_events")).longValue()).isEqualTo(3);
        assertThat(summary(match)).isEqualTo(source(match));
    }

    @Test
    @DisplayName("숫자 자리에 글자가 온 경기도 뷰와 같은 값으로 들어가고, 같이 맞추는 다른 경기를 막지 않는다")
    void badValueDoesNotBlockOthers() {
        String bad = UUID.randomUUID().toString();
        String good = UUID.randomUUID().toString();
        Instant at = Instant.now().minusSeconds(60);
        insertHostEvent(bad, 0, "match_start", at, "{\"player_count\":\"many\",\"partial\":false}");
        insertHostEvent(good, 0, "match_start", at, START);

        summaries.refresh(List.of(bad, good));

        assertThat(((Number) summary(bad).get("player_count")).intValue()).isZero();
        assertThat(summary(bad)).isEqualTo(source(bad));
        assertThat(((Number) summary(good).get("player_count")).intValue()).isEqualTo(2);
    }

    @Test
    @DisplayName("원본에서 행이 사라진 경기는 다시 맞추면 요약에서도 빠진다")
    void refreshFollowsTheSource() {
        String match = UUID.randomUUID().toString();
        insertHostEvent(match, 0, "match_start", Instant.now().minusSeconds(60), START);
        summaries.refresh(List.of(match));
        assertThat(summaryCount(match)).isOne();

        analytics.update("DELETE FROM game_event WHERE match_id = ?", match);
        summaries.refresh(List.of(match));
        assertThat(summaryCount(match)).isZero();
    }

    @Test
    @DisplayName("원본과 어긋난 경기를 찾고, 뜰 때 대조하면 플러셔가 맞춘다")
    void outOfDateMatchesAreFixedOnStartup() throws Exception {
        // 원본에만 있는 경기는 요약을 모르는 옛 코드가 넣은 경기이고(V5 배포 사이), 요약에만 남은 줄은
        // 원본을 지우고 요약을 안 지운 경우입니다.
        String missing = UUID.randomUUID().toString();
        String orphan = UUID.randomUUID().toString();
        Instant at = Instant.now().minusSeconds(60);
        insertHostEvent(orphan, 0, "match_start", at, START);
        summaries.refresh(List.of(orphan));
        analytics.update("DELETE FROM game_event WHERE match_id = ?", orphan);
        insertHostEvent(missing, 0, "match_start", at, START);

        assertThat(summaries.findOutOfDate()).contains(missing, orphan);

        flusher.reconcileOnStartup();
        long deadline = System.currentTimeMillis() + 5_000;
        while ((summaryCount(missing) != 1 || summaryCount(orphan) != 0) && System.currentTimeMillis() < deadline) {
            Thread.sleep(100);
        }
        assertThat(summaryCount(missing)).isOne();
        assertThat(summaryCount(orphan)).isZero();
        assertThat(summaries.findOutOfDate()).doesNotContain(missing, orphan);
    }

    @Test
    @DisplayName("경기를 골라 읽으면 조건이 뷰 안으로 내려가 ix_game_event_match 로 그 경기 행만 읽는다")
    void pickingMatchesUsesTheMatchIndex() {
        // 행이 몇 개뿐이면 옵티마이저가 인덱스보다 전체 읽기를 고를 수 있어 경기 40개를 깝니다.
        List<String> matches = new ArrayList<>();
        Instant at = Instant.now().minusSeconds(60);
        for (int m = 0; m < 40; m++) {
            String match = UUID.randomUUID().toString();
            matches.add(match);
            for (int seq = 0; seq < 25; seq++) {
                insertHostEvent(match, seq, seq == 0 ? "match_start" : "position_sample", at.plusSeconds(seq),
                        seq == 0 ? START : "{\"seat\":0}");
            }
        }
        analytics.execute("ANALYZE TABLE game_event");

        // 경기 둘입니다. 하나면 MySQL 이 실행 전에 미리 계산해 버려 계획에 읽는 방법이 찍히지 않습니다.
        String plan = analytics.queryForObject(
                "EXPLAIN FORMAT=TREE SELECT " + String.join(", ", MatchSummaryWriter.COLUMNS)
                        + " FROM match_analysis_summary_source WHERE match_id IN ('" + matches.get(3) + "', '"
                        + matches.get(17) + "')", String.class);

        // 인덱스 이름만 보면 안 됩니다. 조건이 안 내려가면 계획이 "Index scan on game_event using
        // ix_game_event_match" 가 되는데, 그건 GROUP BY 순서대로 테이블 전체를 훑는다는 뜻입니다.
        // derived_condition_pushdown 을 끄고 확인한 모양입니다. 골라 읽을 때만 range scan 이나 lookup 입니다.
        assertThat(plan).containsPattern("Index (range scan|lookup) on game_event using ix_game_event_match")
                .doesNotContain("Index scan on game_event")
                .doesNotContain("Table scan on game_event");
    }

    private GameEventRequest hostEvent(String session, long seq, String matchId, String name,
                                       long occurredAt, long matchTimeMs, String json) {
        return new GameEventRequest(occurredAt, session, seq, "ABC234", matchId, matchTimeMs,
                UUID.randomUUID().toString(), name, "Searching", "basement", 1.5f, 0f, -2.25f,
                true, (short) 2, params(json));
    }

    private void insertHostEvent(String matchId, long seq, String name, Instant at, String json) {
        LocalDateTime utc = at.atOffset(ZoneOffset.UTC).toLocalDateTime();
        analytics.update("""
                INSERT INTO game_event (occurred_at, received_at, client_session_id, client_seq, room_code, match_id,
                    match_time_ms, user_public_id, event_name, phase, map_id, from_host, schema_ver, params)
                VALUES (?, ?, ?, ?, 'ABC234', ?, ?, NULL, ?, 'Searching', 'basement', 1, 2, ?)
                """, utc, utc, matchId, seq, matchId, seq * 1_000, name, json);
    }

    private RequestBuilder send(String ip, List<GameEventRequest> events) throws Exception {
        return post("/api/v1/events")
                .with(fromIp(ip))
                .contentType(MediaType.APPLICATION_JSON)
                .content(objectMapper.writeValueAsString(events));
    }

    private List<String> columns(String table) {
        return analytics.queryForList("""
                SELECT column_name FROM information_schema.columns
                 WHERE table_schema = ? AND table_name = ? ORDER BY ordinal_position
                """, String.class, ANALYTICS_SCHEMA, table);
    }

    private Map<String, Object> summary(String matchId) {
        return analytics.queryForMap("SELECT * FROM match_analysis_summary WHERE match_id = ?", matchId);
    }

    private Map<String, Object> source(String matchId) {
        return analytics.queryForMap("SELECT * FROM match_analysis_summary_source WHERE match_id = ?", matchId);
    }

    private int summaryCount(String matchId) {
        Integer n = analytics.queryForObject(
                "SELECT COUNT(*) FROM match_analysis_summary WHERE match_id = ?", Integer.class, matchId);
        return n == null ? 0 : n;
    }

    /**
     * 플러셔가 비동기라 도착 건수가 기대치가 될 때까지 최대 5초 기다립니다. 끝내 안 되면 null 입니다.
     * 줄이 생긴 것만 보면 안 됩니다. 요청이 큐에 넣는 도중에 플러셔가 꺼내 가면 한 경기가 두 배치로 나뉩니다.
     */
    private Map<String, Object> awaitSummary(String matchId, long expectedEvents) throws InterruptedException {
        long deadline = System.currentTimeMillis() + 5_000;
        while (System.currentTimeMillis() < deadline) {
            Long received = analytics.query("SELECT received_events FROM match_analysis_summary WHERE match_id = ?",
                    rs -> rs.next() ? rs.getLong(1) : null, matchId);
            if (received != null && received == expectedEvents) {
                return summary(matchId);
            }
            Thread.sleep(100);
        }
        return null;
    }
}
