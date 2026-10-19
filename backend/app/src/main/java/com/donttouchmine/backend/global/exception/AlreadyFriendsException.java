package com.donttouchmine.backend.global.exception;

public class AlreadyFriendsException extends RuntimeException {

    public AlreadyFriendsException() {
        super("이미 친구입니다.");
    }
}
