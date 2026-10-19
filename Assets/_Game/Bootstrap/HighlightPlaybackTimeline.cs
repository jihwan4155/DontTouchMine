using System;
using System.Collections.Generic;
using Game.Core.Match;
using Game.Server.Match;

namespace Game.Bootstrap
{
    internal static class HighlightPlaybackTimeline
    {
        public const double EpsilonSeconds = 0.000001d;
        public static double TotalDuration(IReadOnlyList<HighlightReplayData> replay)
        {
            var total = 0d;
            foreach (var data in replay)
                total += data.Candidate.PlaybackDurationSeconds + HighlightPresentationTiming.OverheadSeconds;
            return total;
        }

        public static bool TryLocate(IReadOnlyList<HighlightReplayData> replay, double elapsed, out int index, out double remaining)
        {
            index = -1;
            remaining = 0d;
            if (elapsed < -EpsilonSeconds) return false;
            elapsed = Math.Max(0d, elapsed);
            for (var replayPosition = 0; replayPosition < replay.Count; replayPosition++)
            {
                var duration = replay[replayPosition].Candidate.PlaybackDurationSeconds +
                    HighlightPresentationTiming.OverheadSeconds;
                if (elapsed < duration)
                {
                    index = replayPosition;
                    remaining = duration - elapsed;
                    return true;
                }

                elapsed -= duration;
            }

            return false;
        }
    }
}
