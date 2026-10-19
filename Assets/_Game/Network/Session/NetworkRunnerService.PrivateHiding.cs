using System;
using Game.Server.Items;
namespace Game.Network.Session
{
    public sealed partial class NetworkRunnerService
    {
        public bool IsSearchingPhase => _matchStarter?.CurrentPhase == Game.Core.Match.MatchPhase.Searching;
        public bool IsPrivateHiding => _matchStarter?.IsPrivateHiding == true;
        public int HidingFirstPlayer => _matchStarter?.HidingFirstPlayer(MatchRules.HidingDurationSeconds) ?? -1;
        public int HidersPerTurn => _matchStarter?.HidersPerTurn ?? 1;
        public PrivateHidingWorlds PrivateHidingWorlds => _matchStarter?.PrivateWorlds;
        public bool IsHidingPlayerComplete(int player) => _matchStarter?.IsHidingPlayerComplete(player) == true;
        public void BindPrivateHidingBodies(Func<PrivateHidingWorlds.Copy, bool> create, Action<string> rollback, Action<int> finish, Func<string,UnityEngine.Pose,bool> releaseClear = null)
        {
            if (_matchStarter == null) return;
            _matchStarter.MaterializePrivateObject=create;
            _matchStarter.RollbackPrivateBody=rollback;
            _matchStarter.FinalizePrivatePlayer=finish;
            _matchStarter.ReleaseBodyIsClear=releaseClear;
        }
    }
}
