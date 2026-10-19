package com.donttouchmine.backend.domain.highlight.dto;

import java.util.List;

/**
 * @param available 모델이 고른 결과인가. false 면 picks 는 비어 있고 게임 서버가 규칙 순위를 그대로 씁니다.
 * @param picks     고른 순서대로의 후보와 그 제목·한 줄 해설
 */
public record HighlightDirectorReply(boolean available, List<Pick> picks) {

    public record Pick(int id, String title, String summary) {
    }

    /** 설정이 없거나 모델 호출이 실패한 경우. 실패해도 오류가 아니라 이 응답입니다. */
    public static HighlightDirectorReply unavailable() {
        return new HighlightDirectorReply(false, List.of());
    }
}
