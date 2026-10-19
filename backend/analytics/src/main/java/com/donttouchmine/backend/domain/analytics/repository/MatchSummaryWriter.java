package com.donttouchmine.backend.domain.analytics.repository;

import lombok.RequiredArgsConstructor;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Isolation;
import org.springframework.transaction.annotation.Transactional;

import java.util.Collection;
import java.util.Collections;
import java.util.List;

/**
 * 경기 요약(match_analysis_summary) 갱신. 배치를 넣은 뒤 그 배치에 든 경기만 다시 계산합니다 (V5).
 *
 * <p>더해 가지 않고 원본에서 다시 셉니다. 재전송된 이벤트는 원본에서 INSERT IGNORE 가 거르므로 요약도
 * 틀어지지 않고, 같은 경기를 몇 번 다시 돌려도 결과가 같습니다. 계산식은 V5 의
 * match_analysis_summary_source 뷰에 있고 여기에는 없습니다.
 *
 * <p>지우고 넣기를 한 트랜잭션으로 묶습니다. 그 사이에 읽는 쪽이 경기가 빠진 목록을 보지 않게 하려는
 * 것입니다. 넣기가 INSERT IGNORE 인 이유는 V5 의 처음 채우기와 같습니다. params 의 숫자 자리에 글자가
 * 온 경기 하나가 엄격 모드 오류로 문장을 멈추면, 같은 배치의 다른 경기까지 요약에 못 들어갑니다.
 */
@Repository
@RequiredArgsConstructor
public class MatchSummaryWriter {

    /** 한 문장에 넣는 경기 수 상한. 부하 테스트처럼 배치마다 경기가 다르면 IN 목록이 배치 크기만큼 길어집니다. */
    static final int CHUNK = 500;

    /** 요약 테이블과 원본 뷰의 컬럼. 순서까지 같아야 하고 MatchSummaryTest 가 둘과 대조합니다. */
    public static final List<String> COLUMNS = List.of(
            "match_id", "room_code", "map_id", "started_at_utc", "last_received_at_utc", "duration_seconds",
            "player_count", "hide_seconds_per_player", "seek_seconds", "stun_hits", "end_reason",
            "received_events", "position_samples", "expected_events", "dropped_samples", "upload_complete");

    private static final String COLUMN_LIST = String.join(", ", COLUMNS);

    /**
     * 요약이 원본과 어긋난 경기. 원본의 경기별 행 수와 요약의 도착 건수를 대조하고, 원본이 사라진 요약 줄도
     * 찾습니다. 앞쪽은 ix_game_event_match 만 읽고(covering index) 뒤쪽은 요약 줄마다 인덱스 한 번이라,
     * 원본 전체를 묶는 요약 계산보다 훨씬 가볍습니다.
     *
     * <p>행 수만 봐도 되는 이유는 원본 행이 바뀌지 않기 때문입니다. 탈퇴 익명화가 고치는 user_public_id 는
     * 요약에 없습니다. 호스트가 아니거나 v1 인 행이 섞인 경기는 원본 뷰가 그 행을 빼고 세므로 늘 어긋나
     * 보이는데, 다시 맞춰도 같은 줄이 나올 뿐이라 해가 없습니다.
     */
    private static final String OUT_OF_DATE = """
            SELECT g.match_id
              FROM (SELECT match_id, COUNT(*) AS n FROM game_event
                     WHERE match_id IS NOT NULL GROUP BY match_id) g
              LEFT JOIN match_analysis_summary s ON s.match_id = g.match_id
             WHERE s.match_id IS NULL OR s.received_events <> g.n
            UNION
            SELECT s.match_id FROM match_analysis_summary s
             WHERE NOT EXISTS (SELECT 1 FROM game_event e WHERE e.match_id = s.match_id)
            """;

    private final JdbcTemplate jdbcTemplate;

    /** 요약을 다시 맞춰야 하는 경기. {@link #OUT_OF_DATE} 참고. */
    public List<String> findOutOfDate() {
        return jdbcTemplate.queryForList(OUT_OF_DATE, String.class);
    }

    /**
     * 이 경기들의 요약을 원본에서 다시 만듭니다. 원본에 행이 없는 경기는 요약에서도 빠집니다.
     *
     * <p>READ COMMITTED 입니다. 기본값(REPEATABLE READ)에서 INSERT ... SELECT 는 읽은 원본 행에 공유 잠금을
     * 겁니다. 그러면 같은 행을 고치는 탈퇴 익명화가 이 갱신을 기다리고, 손으로 전부 다시 맞추는 동안에는
     * 수집 쓰기까지 기다립니다. 이 갱신에는 잠금이 필요 없습니다. 원본에서 다시 세므로 막 커밋된 행을
     * 읽는 것이 오히려 맞습니다.
     */
    @Transactional(isolation = Isolation.READ_COMMITTED)
    public void refresh(Collection<String> matchIds) {
        List<String> ids = List.copyOf(matchIds);
        for (int from = 0; from < ids.size(); from += CHUNK) {
            List<String> chunk = ids.subList(from, Math.min(from + CHUNK, ids.size()));
            String marks = String.join(", ", Collections.nCopies(chunk.size(), "?"));
            Object[] args = chunk.toArray();
            jdbcTemplate.update("DELETE FROM match_analysis_summary WHERE match_id IN (" + marks + ")", args);
            jdbcTemplate.update("INSERT IGNORE INTO match_analysis_summary (" + COLUMN_LIST + ") SELECT "
                    + COLUMN_LIST + " FROM match_analysis_summary_source WHERE match_id IN (" + marks + ")", args);
        }
    }
}
