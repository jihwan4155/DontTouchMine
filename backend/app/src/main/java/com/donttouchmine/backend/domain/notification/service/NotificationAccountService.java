package com.donttouchmine.backend.domain.notification.service;

import com.donttouchmine.backend.domain.user.entity.User;
import com.donttouchmine.backend.domain.user.repository.UserRepository;
import com.donttouchmine.backend.global.exception.SuspendedAccountException;
import com.donttouchmine.backend.global.exception.UnauthorizedException;
import com.donttouchmine.backend.global.exception.UnknownCallerException;
import com.donttouchmine.backend.global.security.AccountTokens;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

/** 알림 연결에 사용할 계정을 확인합니다. WebSocket 프레임과 종료 응답은 처리기가 맡습니다. */
@Service
@RequiredArgsConstructor
public class NotificationAccountService {
    private final UserRepository userRepository;
    private final AccountTokens tokens;

    public int authenticate(String userId, String token) {
        // REST와 같은 순서: 토큰이 틀리면 계정 존재 여부를 조회하지 않습니다.
        // 서명 비밀이 없는 개발 환경의 기존 동작도 유지합니다.
        if (tokens.isEnabled() && !tokens.matches(userId, token)) {
            throw new UnauthorizedException();
        }
        if (userId.isBlank()) {
            throw new UnknownCallerException(userId);
        }
        User user = userRepository.findByPublicId(userId)
                .orElseThrow(() -> new UnknownCallerException(userId));
        if (user.isSuspended()) {
            throw new SuspendedAccountException();
        }
        return user.getSeq();
    }
}
