-- 경기 요약을 뷰에서 테이블로 옮깁니다.
--
-- match_analysis_summary 는 V2·V3 에서 뷰였습니다. 부를 때마다 game_event 전체를 경기별로 다시
-- 묶으므로 행이 쌓일수록 느려집니다. 2026-09-30 운영(40만 행, 474경기)에서 뷰를 한 번 세는 데 12초가
-- 걸렸고, 관리 화면의 경기 목록이 분석 서비스의 쿼리 상한(15초)에 걸려 비어 보였습니다. 분석 탭의 다른
-- 질문도 전부 이 요약에서 경기를 고르므로 같이 끊겼습니다.
--
-- 이제 요약은 경기당 한 줄로 저장해 두고, 수집 서비스가 배치를 넣을 때마다 그 배치에 든 경기만 다시
-- 계산해 덮어씁니다(MatchSummaryWriter). 이름이 같으므로 읽는 쪽(관리 화면, 내부 API, 문서의 SQL)은
-- 바뀌는 것이 없습니다.
--
-- 계산식은 match_analysis_summary_source 뷰 하나에만 둡니다. V3 의 요약 뷰와 같은 정의이고 이름만
-- 다릅니다. 수집 중 갱신, 아래의 처음 채우기, 손으로 다시 맞추기(deploy/README.md)가 모두 이 뷰를
-- 읽습니다. 경기를 골라 읽으면(WHERE match_id IN ...) MySQL 이 그 조건을 뷰 안의 GROUP BY 앞으로
-- 내려 ix_game_event_match 로 그 경기의 행만 읽습니다(derived_condition_pushdown, 8.0.22 부터 기본).
-- 이것이 깨지면 갱신 한 번이 옛 뷰 한 번만큼 느려지므로 MatchSummaryTest 가 실행 계획으로 확인합니다.
--
-- 컬럼 타입은 옛 뷰가 내던 타입 그대로입니다. 읽는 쪽이 받던 값이 달라지지 않게 하려는 것이고,
-- end_reason 이 LONGTEXT 인 것도 그래서입니다(params->>'$.x' 의 결과 타입).

CREATE SQL SECURITY INVOKER VIEW match_analysis_summary_source AS
SELECT match_id,
       MAX(room_code) AS room_code,
       MAX(map_id) AS map_id,
       MIN(occurred_at) AS started_at_utc,
       MAX(received_at) AS last_received_at_utc,
       MAX(match_time_ms) / 1000.0 AS duration_seconds,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.player_count' AS UNSIGNED) END) AS player_count,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.hide_sec' AS UNSIGNED) END) AS hide_seconds_per_player,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.seek_sec' AS UNSIGNED) END) AS seek_seconds,
       MAX(CASE WHEN event_name = 'match_start' THEN CAST(params->>'$.stun_hits' AS UNSIGNED) END) AS stun_hits,
       MAX(CASE WHEN event_name = 'match_end' THEN params->>'$.end_reason' END) AS end_reason,
       COUNT(*) AS received_events,
       SUM(event_name = 'position_sample') AS position_samples,
       MAX(CASE WHEN event_name = 'match_end' THEN CAST(params->>'$.expected_events' AS UNSIGNED) END) AS expected_events,
       MAX(CASE WHEN event_name = 'match_end' THEN CAST(params->>'$.dropped_samples' AS UNSIGNED) END) AS dropped_samples,
       (SUM(event_name = 'match_start') = 1 AND SUM(event_name = 'match_end') = 1
        AND COUNT(*) = MAX(CASE WHEN event_name = 'match_end' THEN CAST(params->>'$.expected_events' AS UNSIGNED) END)
        AND MAX(CASE WHEN event_name IN ('match_start', 'match_end') THEN params->>'$.partial' END) = 'false') AS upload_complete
FROM game_event
WHERE match_id IS NOT NULL AND from_host = 1 AND schema_ver = 2
GROUP BY match_id;

-- 컬럼 순서는 위 뷰와 같습니다. 다시 맞추기가 SELECT * 로 옮기므로 순서가 곧 계약이고,
-- MatchSummaryTest 가 둘과 MatchSummaryWriter.COLUMNS 를 대조합니다.
CREATE TABLE match_analysis_summary_stored (
    match_id                CHAR(36)        NOT NULL,
    room_code               CHAR(6)         NULL,
    map_id                  VARCHAR(32)     NULL,
    started_at_utc          DATETIME(3)     NULL,
    last_received_at_utc    DATETIME(3)     NULL,
    duration_seconds        DECIMAL(15,4)   NULL,
    player_count            BIGINT UNSIGNED NULL,
    hide_seconds_per_player BIGINT UNSIGNED NULL,
    seek_seconds            BIGINT UNSIGNED NULL,
    stun_hits               BIGINT UNSIGNED NULL,
    end_reason              LONGTEXT        NULL,
    received_events         BIGINT          NOT NULL,
    position_samples        DECIMAL(23,0)   NULL,
    expected_events         BIGINT UNSIGNED NULL,
    dropped_samples         BIGINT UNSIGNED NULL,
    upload_complete         INT             NULL,

    PRIMARY KEY (match_id),
    -- 경기 목록의 정렬(started_at_utc DESC)과 기간 필터가 이 컬럼입니다.
    KEY ix_match_summary_started (started_at_utc)
);

-- 처음 채우기. 운영에서는 이 한 문장이 옛 뷰를 한 번 부르는 만큼(십수 초) 걸립니다. 기동 중이라 그동안
-- 수집 요청은 nginx 가 502 로 돌려보내고, 클라이언트가 스풀에 두었다가 같은 이벤트 ID 로 다시 보냅니다.
--
-- IGNORE 인 이유는 엄격 모드입니다. params 의 숫자 자리에 글자가 온 경기는 뷰로 읽으면 경고와 함께 0
-- 이 되지만, INSERT ... SELECT 에서는 같은 변환이 오류가 되어 문장이 멈춥니다. 여기서 멈추면 기동이
-- 실패합니다. IGNORE 는 그 오류를 다시 경고로 돌려 뷰가 보이던 값과 같은 값을 넣습니다.
INSERT IGNORE INTO match_analysis_summary_stored SELECT * FROM match_analysis_summary_source;

DROP VIEW match_analysis_summary;
RENAME TABLE match_analysis_summary_stored TO match_analysis_summary;
