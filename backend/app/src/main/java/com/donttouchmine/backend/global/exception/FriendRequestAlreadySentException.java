package com.donttouchmine.backend.global.exception;

public class FriendRequestAlreadySentException extends RuntimeException {

    public FriendRequestAlreadySentException() {
        super("이미 보낸 친구 요청입니다.");
    }
}
