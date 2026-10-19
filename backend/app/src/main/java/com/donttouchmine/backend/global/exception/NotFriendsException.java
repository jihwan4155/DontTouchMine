package com.donttouchmine.backend.global.exception;

public class NotFriendsException extends RuntimeException {

    public NotFriendsException() {
        super("친구가 아닙니다.");
    }
}
