package com.donttouchmine.backend.domain.highlight.dto;

import jakarta.validation.constraints.DecimalMax;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;

/**
 * 하이라이트 후보 하나. 게임 서버가 규칙으로 고른 장면을 보냅니다.
 *
 * <p>이 값은 그대로 모델 프롬프트에 들어갑니다. 그래서 eventType 을 정해진 이름으로만 받습니다.
 * 자유 문자열을 받으면 프롬프트에 지시문을 끼워 넣을 수 있습니다.
 *
 * @param id               요청 안에서의 후보 번호. 모델은 이 번호로만 고르고, 응답이 이 밖의 번호를
 *                         고르면 버립니다.
 * @param eventType        사건 종류
 * @param seconds          장면 길이(초)
 * @param segments         장면이 이어 붙인 구간 수
 * @param remainingSeconds 사건 시점의 남은 경기 시간(초)
 * @param involvedPlayers  사건에 얽힌 사람 수
 * @param ruleScore        규칙이 매긴 점수
 */
public record HighlightCandidate(
        @Min(0) @Max(9)
        int id,

        @NotNull
        @Pattern(regexp = "FirstBlood|TteTanMulgun|FinalMoment|LongestHidden|MostStunned"
                + "|ItemDestroyed|PlayerStunned|ItemRecovered")
        String eventType,

        @DecimalMin("0.01") @DecimalMax("120")
        double seconds,

        @Min(1) @Max(8)
        int segments,

        @DecimalMin("0") @DecimalMax("86400")
        double remainingSeconds,

        @Min(0) @Max(6)
        int involvedPlayers,

        @DecimalMin("0") @DecimalMax("1000")
        double ruleScore
) {
}
