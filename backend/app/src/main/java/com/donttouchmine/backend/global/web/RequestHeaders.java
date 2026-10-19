package com.donttouchmine.backend.global.web;

/**
 * 게임 API 가 받는 헤더 이름.
 *
 * <p>컨트롤러와 인터셉터가 각자 문자열을 적어 두면, 한 곳만 바뀌었을 때 인터셉터가 보는 헤더와
 * 컨트롤러가 요구하는 헤더가 갈라집니다. 그러면 검사는 조용히 아무것도 막지 않습니다.
 */
public final class RequestHeaders {

    /**
     * 호출자를 알아내는 헤더. 값은 users.public_id 입니다.
     *
     * <p><b>이것만으로는 식별이지 인증이 아닙니다.</b> 짝이 되는 {@link #ACCOUNT_TOKEN_HEADER} 를
     * AccountTokenInterceptor 가 확인합니다.
     */
    public static final String USER_ID_HEADER = "X-User-Id";

    /** 계정 응답에서 받은 토큰. X-User-Id 가 정말 그 사람의 것인지 증명합니다. */
    public static final String ACCOUNT_TOKEN_HEADER = "X-Account-Token";

    private RequestHeaders() {
    }
}
