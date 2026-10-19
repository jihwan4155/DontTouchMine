package com.donttouchmine.backend.domain.highlight.dto;

import com.fasterxml.jackson.annotation.JsonIgnore;
import jakarta.validation.Valid;
import jakarta.validation.constraints.AssertTrue;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.util.List;
import java.util.Objects;

/**
 * @param candidates 고를 후보. 1~10개이고 id 가 겹치면 안 됩니다.
 */
public record HighlightDirectorRequest(
        @NotNull @Size(min = 1, max = 10)
        List<@NotNull @Valid HighlightCandidate> candidates
) {

    /**
     * 후보 id 가 서로 다른가. 모델 응답을 id 로 후보에 되돌려 붙이므로 겹치면 어느 장면인지 모릅니다.
     *
     * <p>요청 검증에 두어 다른 형식 오류와 같은 400 INVALID_REQUEST 로 나가게 합니다. null 목록과
     * null 원소는 위 애너테이션이 따로 거르므로 여기서는 통과시킵니다.
     *
     * <p>{@code @JsonIgnore} 는 이 검사가 API 명세에 요청 속성처럼 실리지 않게 합니다.
     */
    @JsonIgnore
    @AssertTrue(message = "후보 id 가 겹칩니다.")
    public boolean isDistinctIds() {
        if (candidates == null) {
            return true;
        }
        List<HighlightCandidate> present = candidates.stream().filter(Objects::nonNull).toList();
        return present.stream().map(HighlightCandidate::id).distinct().count() == present.size();
    }
}
