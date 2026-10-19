using Game.Core.Match;
using System;
using System.Collections.Generic;
using Game.Client.Interactions;
using Game.Network.Match;
using Game.Network.Players;
using UnityEngine;
namespace Game.Bootstrap
{
    public sealed partial class NetworkInteractionSceneBridge
    {
        private readonly Dictionary<PlayerAvatar,int> privatePhysicsGroup=new();
        private Game.Server.Match.IMatchRuntimeContext privateReplayContext;
        public void BindPrivateReplayContext(Game.Server.Match.IMatchRuntimeContext context)=>privateReplayContext=context;
        private PhysicsPlacementValidator privatePlacement;
        private SparseHidingObjects privateObjects;
        private readonly Dictionary<PlayerAvatar,PrivatePlayerVisibility> privateVisibility=new();
        private readonly Dictionary<CarryableItem,PrivatePlayerVisibility> privateAssignedVisibility=new();
        public void BindPrivatePlacement(PhysicsPlacementValidator validator)=>privatePlacement=validator;
        private void StartPrivateHiding()
        {
            privateObjects=new SparseHidingObjects(items,scene,privatePlacement);
            if(network.IsServer)network.BindPrivateHidingBodies(privateObjects.Materialize,privateObjects.Rollback,FinalizePrivatePlayer,privatePlacement == null ? null : privatePlacement.IsClear);
        }
        private void DisposePrivateHiding()
        {
            if(!lobbyMode&&network.IsServer)network.BindPrivateHidingBodies(null,null,null);
            foreach(var view in privateVisibility.Values)if(view!=null)view.SetHidden(false);
            foreach(var view in privateAssignedVisibility.Values)if(view!=null)view.SetHidden(false);
            privateAssignedVisibility.Clear();
            privateVisibility.Clear();privateObjects?.Dispose();privateObjects=null;
        }
        private void UpdatePrivateHiding()
        {
            if(privateObjects==null||!network.IsPrivateHiding)return;
            privateObjects.BeginPair(Math.Max(0,network.HidingFirstPlayer)/2);
        }
        private bool InCurrentPair(int player)
        {
            var first=Math.Max(0,network.HidingFirstPlayer);
            return player>=first&&player<first+2&&!network.IsHidingPlayerComplete(player);
        }
        private void ApplyPrivatePlayerView(PlayerAvatar avatar,int index,NetworkPlayerMotor motor)
        {
            var hiding=!lobbyMode&&network.IsPrivateHiding;
            bool actorHiding=hiding&&InCurrentPair(index);
            int group=actorHiding ? index&1 : hiding ? -2 : -1;
            motor?.SetPrivateHidingGroup(group);
            if(!privatePhysicsGroup.TryGetValue(avatar,out var oldGroup)||oldGroup!=group)
            {
                privatePhysicsGroup[avatar]=group;
                int layer=group<0 ? LayerMask.NameToLayer("Player") : HidingPhysicsGroups.LayerFor(index);
                foreach(var collider in avatar.GetComponentsInChildren<Collider>(true))
                    if(collider.GetComponentInParent<CarryableItem>()==null)collider.gameObject.layer=layer;
            }
            var interactor=avatar.GetComponent<PlayerInteractor>();
            if(interactor!=null)interactor.InteractionMask=actorHiding ?
                HidingPhysicsGroups.QueryMask(HidingPhysicsGroups.LayerFor(index)) :
                hiding ? Physics.DefaultRaycastLayers & ~HidingPhysicsGroups.Mask : Physics.DefaultRaycastLayers;
            if(!privateVisibility.TryGetValue(avatar,out var visibility)||visibility==null)
            {
                visibility=avatar.GetComponent<PrivatePlayerVisibility>()??avatar.gameObject.AddComponent<PrivatePlayerVisibility>();
                visibility.IncludesChild=node=>node.GetComponentInParent<CarryableItem>()==null;
                privateVisibility[avatar]=visibility;
            }
            visibility.SetHidden(hiding&&!avatar.IsOwner&&(actorHiding||InCurrentPair(room.LocalPlayerIndex)));
        }
        private void PreparePrivateObjects(IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            if(privateObjects==null||states==null||network.HidersPerTurn!=2)return;
            if(network.IsPrivateHiding)
            {
                privateObjects.BeginPair(Math.Max(0,network.HidingFirstPlayer)/2);
                if(!network.IsServer)
                    foreach(var state in states)
                        if(state.PrivateOwner==room.LocalPlayerIndex && InCurrentPair(state.PrivateOwner))privateObjects.ShowPrivate(state);
                foreach(var state in states)
                    if (state.PrivateOwner >= 0 && items.TryGetValue(state.ObjectId, out var item) && item != null)
                    {
                        if(network.IsServer && string.IsNullOrEmpty(state.SourceObjectId))
                        {
                            if(!privateAssignedVisibility.TryGetValue(item,out var view) || view==null)
                                privateAssignedVisibility[item]=view=item.gameObject.AddComponent<PrivatePlayerVisibility>();
                            view.SetHidden(state.PrivateOwner!=room.LocalPlayerIndex);
                        }
                        int layer = HidingPhysicsGroups.LayerFor(state.PrivateOwner);
                        item.SetInteractionLayer(layer);
                        privatePlacement?.RegisterPrivateCopy(state.ObjectId, string.IsNullOrEmpty(state.SourceObjectId) ? state.ObjectId : state.SourceObjectId, layer);
                    }
            }
            else
            {
                foreach(var state in states)if(state.PrivateOwner>=0)return;
                foreach(var view in privateAssignedVisibility.Values)if(view!=null)view.SetHidden(false);
                bool hasResults=false;
                foreach(var state in states)hasResults|=!string.IsNullOrEmpty(state.SourceObjectId);
                if((hasResults || network.IsSearchingPhase) && privateObjects.Merge(states))
                {
                    appliedVersions.Clear();
                    PlaygroundMatchScene.RegisterPrivateReplayItems(privateReplayContext,items,states);
                }
                foreach(var state in states)
                    if(items.TryGetValue(state.ObjectId,out var item) && item!=null)
                        item.SetInteractionLayer(LayerMask.NameToLayer("Carryable"));
            }
        }
        private string PrivateHoldBlockReason(string id)
        {
            if(lobbyMode || !network.IsPrivateHiding || id==assignedItemId)return null;
            if(!InCurrentPair(room.LocalPlayerIndex))return "숨기기 차례에 물건을 옮길 수 있어요";
            int count=0;
            foreach(var state in objectStates)
            {
                if(state.PrivateOwner!=room.LocalPlayerIndex || string.IsNullOrEmpty(state.SourceObjectId))continue;
                if(state.ObjectId==id || state.SourceObjectId==id)return null;
                count++;
            }
            return count>=Game.Server.Items.PrivateHidingWorlds.MaxObjectsPerPlayer ? "공용 물건은 32개까지 옮길 수 있어요" : null;
        }
        private void FinalizePrivatePlayer(int player)
        {
            foreach(var state in objectStates)
            {
                if(state.PrivateOwner!=player||state.HolderPlayerIndex>=0||state.IsDestroyed||
                    !items.TryGetValue(state.ObjectId,out var item)||item==null)continue;
                var pose=new Pose(item.transform.position,item.transform.rotation);
                if(privatePlacement != null && !privatePlacement.IsValid(state.ObjectId, pose))
                {
                    if(privatePlacement.TryGround(state.ObjectId, pose, out var grounded)) pose = grounded;
                    else if(privateObjects.TryBaselinePose(state.SourceObjectId, out var baseline)) pose = baseline;
                    else Debug.LogWarning($"[PrivateHiding] No supported final pose for '{state.ObjectId}'.");
                }
                if(network.TryConfirmObjectPhysicsPose(state.ObjectId,pose,Vector3.zero,false,state.Version))
                    appliedVersions[state.ObjectId]=state.Version+1;
                item.OnSettled(pose,false);
            }
        }
    }
}
