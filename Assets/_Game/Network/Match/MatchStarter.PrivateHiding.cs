using System;
using Fusion;
using Game.Server.Items;
using UnityEngine;
namespace Game.Network.Match
{
    public sealed partial class MatchStarter
    {
        public bool IsPrivateHiding => _state != null && _state.UsesPrivateObjects;
        public int HidingFirstPlayer(double turnSeconds) => _state == null ? -1 :
            Game.Core.Match.HidingTurns.IndexAt(_state.Phase, _state.PhaseEndsAt, ServerTime,
                _state.ParticipantCount, turnSeconds, HidersPerTurn);
        public Func<PrivateHidingWorlds.Copy, bool> MaterializePrivateObject;
        public Action<string> RollbackPrivateBody;
        public Action<int> FinalizePrivatePlayer;
        public Func<string,Pose,bool> ReleaseBodyIsClear;
        internal void FinalizeHidingPlayer(int player) => FinalizePrivatePlayer?.Invoke(player);
        internal void PublishPrivateWorlds() => _session?.PublishPrivateWorlds();
        public bool IsHidingPlayerComplete(int player) => _state != null && (_state.CompletedHiderMask & (1 << player)) != 0;
        public PrivateHidingWorlds PrivateWorlds => _session?.PrivateWorlds;
        public int HidersPerTurn => _state != null ? Math.Max(1, _state.HidersPerTurn) : 1;
        internal int PrivateOwnerOf(string objectId)
        {
            if (_session == null) return -1;
            if (PrivateWorlds != null && PrivateWorlds.TryGetCopy(objectId, out var copy)) return copy.PlayerIndex;
            for (int i=0; i<_session.Assignments.Count; i++)
                if (_session.Assignments[i].Item.ItemId == objectId) return i;
            return -1;
        }
        internal string PrivateSourceOf(string objectId) => PrivateWorlds != null &&
            PrivateWorlds.TryGetCopy(objectId, out var copy) ? copy.SourceId : string.Empty;
        internal bool TryPrivatePlayerIndex(PlayerRef source, out int index) => TryGetPlayerIndex(source, out index);
        internal bool TryPrivateRecipient(int playerIndex, out PlayerRef recipient)
        {
            recipient = default;
            if (_roster == null || playerIndex < 0) return false;
            foreach (var avatar in _roster.Avatars)
                if (avatar != null && TryGetPlayerIndex(avatar.Owner, out var index) && index == playerIndex)
                { recipient = avatar.Owner; return true; }
            return false;
        }
        internal int HidingCompletedMask
        {
            get
            {
                int mask=0;
                if (_session != null) for(int i=0;i<_session.Assignments.Count;i++)
                    if (_session.IsHidingComplete(i)) mask |= 1 << i;
                return mask;
            }
        }
        internal bool TryGetFinalPrivatePose(string objectId, out Pose pose)
        {
            pose = default;
            if (PrivateWorlds == null || !PrivateWorlds.TryGetCopy(objectId, out var copy)) return false;
            pose = copy.Pose; return true;
        }
    }
}
