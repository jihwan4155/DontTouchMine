package com.donttouchmine.backend.domain.highlight.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.donttouchmine.backend.domain.highlight.dto.HighlightDirectorReply;
import com.donttouchmine.backend.domain.highlight.dto.HighlightDirectorRequest;
import com.donttouchmine.backend.domain.highlight.service.HighlightDirectorService;
import com.donttouchmine.backend.global.security.AccountTokens;

import static com.donttouchmine.backend.global.web.RequestHeaders.USER_ID_HEADER;

/**
 * 경기 하이라이트 후보 중 보여 줄 것을 모델에게 고르게 합니다 (GAME-1015).
 *
 * <p>X-User-Id 와 토큰의 짝은 AccountTokenInterceptor 가 이미 확인했습니다. 여기서는 헤더가 있는지만
 * 요구합니다. 신원을 대지 않은 호출이 모델 호출 비용을 쓰지 못하게 하려는 것입니다.
 */
@RestController
@RequestMapping("/api/v1/highlights/director")
@RequiredArgsConstructor
public class HighlightDirectorController {

    private final HighlightDirectorService service;
    private final AccountTokens tokens;

    /**
     * 실패는 오류가 아니라 {@code available=false} 입니다. 게임 서버는 그때 규칙 순위를 그대로 씁니다.
     *
     * <p>토큰 비밀이 없는 서버도 모델을 부르지 않습니다. 그 서버는 인터셉터가 토큰을 확인하지 않아
     * 아무나 X-User-Id 를 대고 부를 수 있습니다.
     */
    @PostMapping
    public HighlightDirectorReply direct(@RequestHeader(USER_ID_HEADER) String userId,
                                         @Valid @RequestBody HighlightDirectorRequest request) {
        if (!service.enabled() || !tokens.isEnabled()) {
            return HighlightDirectorReply.unavailable();
        }
        return service.direct(request.candidates());
    }
}
