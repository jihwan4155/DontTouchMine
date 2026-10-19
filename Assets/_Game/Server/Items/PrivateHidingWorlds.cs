using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Server.Items
{
    /// <summary>One match's bounded, independent hiding edits. Original poses never change.</summary>
    public sealed class PrivateHidingWorlds
    {
        public const int MaxObjectsPerPlayer = 32;
        private readonly Dictionary<string, WorldObjectState> originals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> sourceIndices = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Copy> copies = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string>[] idsByPlayer;
        private readonly bool[] completed;
        private bool published;

        public readonly struct Copy
        {
            public Copy(int playerIndex, string sourceId, string objectId, Pose pose)
            {
                PlayerIndex = playerIndex; SourceId = sourceId; ObjectId = objectId; Pose = pose;
            }
            public int PlayerIndex { get; }
            public string SourceId { get; }
            public string ObjectId { get; }
            public Pose Pose { get; }
        }

        public PrivateHidingWorlds(int playerCount, IReadOnlyList<WorldObjectState> initial)
        {
            if (playerCount < 1 || playerCount > 6) throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            idsByPlayer = new Dictionary<string, string>[playerCount];
            completed = new bool[playerCount];
            for (int p = 0; p < playerCount; p++) idsByPlayer[p] = new(StringComparer.Ordinal);
            foreach (var state in initial)
                if (string.IsNullOrWhiteSpace(state.ObjectId) || !originals.TryAdd(state.ObjectId, state))
                    throw new ArgumentException("Duplicate or empty original id.", nameof(initial));
            var ordered = new List<string>(originals.Keys);
            ordered.Sort(StringComparer.Ordinal);
            for (int i = 0; i < ordered.Count; i++)
            {
                sourceIndices.Add(ordered[i], i);
                for (int p = 0; p < playerCount; p++)
                    if (originals.ContainsKey(Id(p, i)))
                        throw new ArgumentException("Original id uses reserved hiding namespace.", nameof(initial));
            }
        }

        public int Count => copies.Count;
        public bool IsPublished => published;
        public IEnumerable<Copy> Copies => copies.Values;
        private static string Id(int player, int source) => $"~{player:X1}{source:X8}";

        // Reserve before materializing physics or network state; roll back if either fails.
        public bool TryReserve(int player, string sourceId, out Copy copy, out bool created)
        {
            copy = default; created = false;
            if (published || player < 0 || player >= idsByPlayer.Length || completed[player] ||
                sourceId == null || !originals.TryGetValue(sourceId, out var original)) return false;
            if (idsByPlayer[player].TryGetValue(sourceId, out var existing))
            {
                copy = copies[existing]; return true;
            }
            if (idsByPlayer[player].Count >= MaxObjectsPerPlayer) return false;
            var id = Id(player, sourceIndices[sourceId]);
            copy = new Copy(player, sourceId, id, original.Pose);
            copies.Add(id, copy);
            idsByPlayer[player].Add(sourceId, id);
            created = true;
            return true;
        }

        public bool RollbackReservation(int player, string objectId)
        {
            if (published || player < 0 || player >= completed.Length || completed[player] ||
                objectId == null || !copies.TryGetValue(objectId, out var copy) || copy.PlayerIndex != player)
                return false;
            copies.Remove(objectId);
            idsByPlayer[player].Remove(copy.SourceId);
            return true;
        }

        public bool TryGetCopy(int player, string sourceId, out Copy copy)
        {
            copy = default;
            return player >= 0 && player < idsByPlayer.Length && sourceId != null &&
                idsByPlayer[player].TryGetValue(sourceId, out var id) && copies.TryGetValue(id, out copy);
        }

        public bool TryGetCopy(string objectId, out Copy copy)
        {
            copy = default;
            return objectId != null && copies.TryGetValue(objectId, out copy);
        }

        public bool TryGetOriginal(string sourceId, out WorldObjectState original)
        {
            original = default;
            return sourceId != null && originals.TryGetValue(sourceId, out original);
        }

        public bool TryRecordPose(int player, string objectId, Pose pose)
        {
            if (published || player < 0 || player >= completed.Length || completed[player] ||
                !TryGetCopy(objectId, out var copy) || copy.PlayerIndex != player || !Valid(pose)) return false;
            copies[objectId] = new Copy(player, copy.SourceId, objectId, pose);
            return true;
        }

        public bool Complete(int player)
        {
            if (published || player < 0 || player >= completed.Length || completed[player]) return false;
            completed[player] = true;
            return true;
        }

        public bool IsComplete(int player) => player >= 0 && player < completed.Length && completed[player];

        public bool TryPublish(out Copy[] results)
        {
            results = null;
            if (published) return false;
            foreach (var done in completed) if (!done) return false;
            results = new Copy[copies.Count];
            copies.Values.CopyTo(results, 0);
            Array.Sort(results, (a, b) => string.CompareOrdinal(a.ObjectId, b.ObjectId));
            published = true;
            return true;
        }

        private static bool Valid(Pose pose)
        {
            var p = pose.position; var q = pose.rotation;
            return float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z) &&
                float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w) &&
                Mathf.Abs(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w - 1f) <= 0.01f;
        }
    }
}
