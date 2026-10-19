using System;
using System.Collections.Generic;
using Fusion;
using Game.Core.Match;
using UnityEngine;

namespace Game.Network.Match
{
    public sealed partial class MatchSessionState
    {
        [Networked] public int HidersPerTurn { get; set; }
        [Networked] public int MatchEpoch { get; set; }
        [Networked] public int CompletedHiderMask { get; set; }
        private readonly List<ReplicatedObjectState> privateObjects = new(MaxReplicatedObjects);
        private readonly HashSet<int> privateChanged = new();
        private readonly Dictionary<string, ReplicatedObjectState> receivedPrivateObjects = new(StringComparer.Ordinal);
        private readonly Dictionary<PlayerRef, double> privateRequestTimes = new();
        private bool privateDirty;
        private int receivedPrivateEpoch = -1;
        private double nextPrivateRequest;
        internal bool UsesPrivateObjects => IsStarted && HidersPerTurn == 2 && Phase == MatchPhase.Hiding;
        internal int WritableObjectCount => UsesPrivateObjects ? privateObjects.Count : ObjectStateCount;
        internal ReplicatedObjectState ReadAuthorityObject(int index) =>
            UsesPrivateObjects ? privateObjects[index] : ObjectStates.Get(index);

        private readonly Dictionary<PlayerRef, (int epoch, int sequence)> interactionRequests = new();
        private bool AcceptInteractionRequest(PlayerRef source, int epoch, MatchPhase phase, double endsAt, int sequence)
        {
            if (epoch != MatchEpoch || phase != Phase || endsAt != PhaseEndsAt || sequence <= 0) return false;
            if (interactionRequests.TryGetValue(source, out var previous) && previous.epoch == epoch && sequence <= previous.sequence)
                return false;
            interactionRequests[source] = (epoch, sequence);
            return true;
        }

        private void ClearPrivateObjects()
        {
            privateObjects.Clear(); privateChanged.Clear(); receivedPrivateObjects.Clear(); privateRequestTimes.Clear();
            interactionRequests.Clear();
            privateDirty = false; receivedPrivateEpoch = -1; nextPrivateRequest = 0;
            CompletedHiderMask = 0;
        }

        private void EnterPrivateHiding()
        {
            privateObjects.Clear(); privateChanged.Clear();
            var starter = StarterOf(Runner);
            for (int i = 0; i < ObjectStateCount; i++)
            {
                var state = ObjectStates.Get(i);
                state.PrivateOwner = starter?.PrivateOwnerOf(state.ObjectId.ToString()) ?? -1;
                if (state.PrivateOwner >= 0) { privateChanged.Add(privateObjects.Count); privateObjects.Add(state); }
                ObjectStates.Set(i, default);
            }
            ObjectStateCount = 0; ObjectStateRevision++; privateDirty = true;
        }

        private void PublishHidingResults()
        {
            // Every edited source retains every owner's final result; no last-writer-wins merge.
            var starter = StarterOf(Runner);
            foreach (var entry in privateObjects)
            {
                var state = entry;
                if (starter != null && starter.TryGetFinalPrivatePose(state.ObjectId.ToString(), out var pose))
                { state.Position = pose.position; state.Rotation = pose.rotation; }
                state.HolderPlayerIndex = -1; state.InitialVelocity = default;
                state.IsPhysicsActive = false; state.IsPendingEjection = false;
                state.PrivateOwner = -1; state.Version++;
                ObjectStates.Set(ObjectStateCount++, state);
            }
            starter?.PublishPrivateWorlds();
            privateObjects.Clear(); privateChanged.Clear(); privateDirty = false;
            ObjectStateRevision++;
        }

        private static MatchObjectStateSnapshot Snapshot(ReplicatedObjectState state) =>
            new(state.ObjectId.ToString(), state.HolderPlayerIndex, new Pose(state.Position, state.Rotation),
                state.InitialVelocity, state.IsDestroyed, state.Version, state.IsPhysicsActive,
                state.IsPendingEjection, state.SourceObjectId.ToString(), state.PrivateOwner);

        private void PublishPrivateObjects()
        {
            _publishedObjectStateRevision = ObjectStateRevision;
            var authority = Object != null && Object.HasStateAuthority;
            var result = new MatchObjectStateSnapshot[authority ? privateObjects.Count : receivedPrivateObjects.Count];
            int index = 0;
            if (authority)
            {
                foreach (var state in privateObjects) result[index++] = Snapshot(state);
                var starter = StarterOf(Runner);
                foreach (var changed in privateChanged)
                {
                    var state = privateObjects[changed];
                    if (starter != null && starter.TryPrivateRecipient(state.PrivateOwner, out var target))
                        RPC_PrivateObject(target, MatchEpoch, state);
                }
                privateChanged.Clear();
            }
            else foreach (var state in receivedPrivateObjects.Values) result[index++] = Snapshot(state);
            privateDirty = false;
            StarterOf(Runner)?.PublishObjectStates(result);
        }

        private void TickPrivateReplication()
        {
            if (!UsesPrivateObjects) return;
            if (Object.HasStateAuthority)
            {
                CompletedHiderMask = StarterOf(Runner)?.HidingCompletedMask ?? 0;
            }
            else
            {
                if (receivedPrivateEpoch != MatchEpoch)
                {
                    receivedPrivateObjects.Clear(); receivedPrivateEpoch = MatchEpoch;
                    privateDirty = true; nextPrivateRequest = 0;
                }
                // Recover scene-load ordering and reconnects; requests are rate-limited on authority too.
                if (Time.unscaledTimeAsDouble >= nextPrivateRequest)
                { nextPrivateRequest = Time.unscaledTimeAsDouble + 1; RPC_RequestPrivateObjects(MatchEpoch); }
            }
            if (privateDirty) PublishPrivateObjects();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PrivateObject([RpcTarget] PlayerRef target, int epoch, ReplicatedObjectState state)
        {
            if (Object.HasStateAuthority || !UsesPrivateObjects || epoch != MatchEpoch) return;
            if (receivedPrivateEpoch != epoch) { receivedPrivateObjects.Clear(); receivedPrivateEpoch = epoch; }
            var key = state.ObjectId.ToString();
            if (receivedPrivateObjects.TryGetValue(key, out var previous) && previous.Version >= state.Version) return;
            if (!receivedPrivateObjects.ContainsKey(key) && receivedPrivateObjects.Count >= 33) return;
            receivedPrivateObjects[key] = state; privateDirty = true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        private void RPC_RequestPrivateObjects(int epoch, RpcInfo info = default)
        {
            var starter = StarterOf(Runner);
            if (!UsesPrivateObjects || epoch != MatchEpoch || starter == null ||
                !starter.TryPrivatePlayerIndex(info.Source, out var player)) return;
            var now = Runner.SimulationTime;
            if (privateRequestTimes.TryGetValue(info.Source, out var previous) && now - previous < 0.9) return;
            privateRequestTimes[info.Source] = now;
            foreach (var state in privateObjects)
                if (state.PrivateOwner == player) RPC_PrivateObject(info.Source, MatchEpoch, state);
        }
    }
}
