using System;

namespace Game.Server.Match
{
    internal static class HighlightReplayCapture
    {
        public static HighlightReplayClip[] Capture(HighlightReplayBuffer buffer, HighlightCandidate highlight, double sampleIntervalSeconds)
        {
            var clips = new HighlightReplayClip[highlight.Segments.Count];
            for (var index = 0; index < highlight.Segments.Count; index++)
            {
                var segment = highlight.Segments[index];
                clips[index] = new HighlightReplayClip(
                    segment,
                    CaptureFrames(buffer, segment, sampleIntervalSeconds));
            }

            return clips;
        }

        private static HighlightReplayFrame[] CaptureFrames(HighlightReplayBuffer buffer, HighlightSegment segment, double sampleIntervalSeconds)
        {
            var captured = buffer.CaptureWithBoundary(
                segment.StartedAt,
                segment.EndedAt);
            var maxFrameCount = Math.Max(
                2,
                (int)Math.Ceiling(
                    segment.PlaybackDurationSeconds /
                    sampleIntervalSeconds) + 1);
            if (captured.Length <= maxFrameCount)
            {
                return captured;
            }

            var sampled = new HighlightReplayFrame[maxFrameCount];
            for (var index = 0; index < sampled.Length; index++)
            {
                var sourceIndex = index * (captured.Length - 1) /
                                  (sampled.Length - 1);
                sampled[index] = captured[sourceIndex];
            }

            return sampled;
        }

    }
}
