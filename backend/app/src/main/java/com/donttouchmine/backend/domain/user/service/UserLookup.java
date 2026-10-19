package com.donttouchmine.backend.domain.user.service;

import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

import com.donttouchmine.backend.domain.user.entity.User;
import com.donttouchmine.backend.domain.user.repository.UserRepository;
import com.donttouchmine.backend.global.exception.TargetUserNotFoundException;
import com.donttouchmine.backend.global.exception.UnknownCallerException;

/**
 * 공개 식별자로 계정을 찾습니다. 누가 찾느냐에 따라 없을 때의 응답이 다릅니다.
 *
 * <p>부르는 사람이 없으면 404 ACCOUNT_NOT_FOUND 이고 클라이언트는 계정 발급을 다시 부릅니다.
 * 상대가 없으면 404 TARGET_NOT_FOUND 이고 클라이언트는 화면만 정리합니다. 두 코드를 섞으면 상대가
 * 탈퇴했을 뿐인데 내 계정을 새로 발급받는 일이 생깁니다.
 *
 * <p>서비스마다 같은 두 메서드를 복사해 두던 것을 여기로 모았습니다. 복사본 하나에서만 예외를
 * 바꾸면 같은 상황이 API 마다 다른 코드로 나갑니다.
 */
@Component
@RequiredArgsConstructor
public class UserLookup {

    private final UserRepository userRepository;

    /** 요청을 보낸 사람(X-User-Id). 없으면 {@link UnknownCallerException}. */
    public User caller(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new UnknownCallerException(userId));
    }

    /**
     * 요청이 가리키는 상대. 없으면 {@link TargetUserNotFoundException}.
     *
     * <p>운영자 화면이 고른 계정도 여기입니다. 그때 부르는 사람은 로그인한 운영자라 ACCOUNT_NOT_FOUND 가
     * 맞지 않습니다.
     */
    public User target(String userId) {
        return userRepository.findByPublicId(userId)
                .orElseThrow(() -> new TargetUserNotFoundException(userId));
    }
}
