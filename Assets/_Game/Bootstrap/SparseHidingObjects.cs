using Game.Core.Match;
using System;
using System.Collections.Generic;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Network.Match;
using Game.Server.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Game.Bootstrap
{
    // Sparse bodies only: untouched map props remain shared and kinematic.
    internal sealed class SparseHidingObjects : IDisposable
    {
        private sealed class Split
        {
            public string SourceId;
            public CarryableItem Original, Duplicate;
            public Pose Pose;
            public int Layer, FirstPlayer;
            public string OriginalAlias, DuplicateAlias;
            public ReplayVisual BaselineView;
        }
        private readonly Dictionary<string, CarryableItem> items;
        private readonly Dictionary<string, Split> splits = new(StringComparer.Ordinal);
        private readonly List<CarryableItem> mergedCopies = new();
        private readonly PhysicsPlacementValidator validator;
        private readonly Scene scene;
        private int pair = -1;
        private bool merged;
        private readonly Dictionary<Rigidbody,bool> originalMotion = new();
        private readonly Dictionary<CarryableItem, Renderer[]> renderers = new();
        private readonly Dictionary<Renderer, bool> visibility = new();
        private void ShowBody(CarryableItem item, bool show)
        {
            if(item==null)return;
            if(!renderers.TryGetValue(item,out var cached))
            {
                cached=item.GetComponentsInChildren<Renderer>(true); renderers[item]=cached;
                foreach(var renderer in cached) visibility.TryAdd(renderer,renderer.forceRenderingOff);
            }
            foreach(var renderer in cached) if(renderer!=null) renderer.forceRenderingOff=!show || visibility[renderer];
        }
        public void ShowHostView(int localPlayer)
        {
            if(merged || localPlayer<0)return;
            foreach(var split in splits.Values)
            {
                var ownDuplicate=split.FirstPlayer==localPlayer;
                var ownOriginal=split.OriginalAlias!=null && (split.FirstPlayer^1)==localPlayer;
                if(!ownDuplicate&&!ownOriginal && split.BaselineView==null)
                {
                    split.BaselineView=new ReplayVisual(split.Original.transform,null);
                    var originals=split.Original.GetComponentsInChildren<Renderer>(true);
                    var copies=split.BaselineView.Target.GetComponentsInChildren<Renderer>(true);
                    for(int i=0;i<Math.Min(originals.Length,copies.Length);i++)
                    {
                        copies[i].shadowCastingMode=originals[i].shadowCastingMode;
                        copies[i].lightmapIndex=originals[i].lightmapIndex;
                        copies[i].lightmapScaleOffset=originals[i].lightmapScaleOffset;
                    }
                    split.BaselineView.Target.SetPositionAndRotation(split.Pose.position,split.Pose.rotation);
                    SceneManager.MoveGameObjectToScene(split.BaselineView.Target.gameObject,scene);
                }
                split.BaselineView?.SetPlaying(!ownDuplicate&&!ownOriginal);
                ShowBody(split.Original,ownOriginal); ShowBody(split.Duplicate,ownDuplicate);
            }
        }
        public SparseHidingObjects(Dictionary<string, CarryableItem> items, Scene scene, PhysicsPlacementValidator validator)
        {
            this.items=items; this.scene=scene; this.validator=validator;
            Physics.IgnoreLayerCollision(HidingPhysicsGroups.FirstLayer, HidingPhysicsGroups.SecondLayer, true);
            Physics.IgnoreLayerCollision(HidingPhysicsGroups.FirstLayer, LayerMask.NameToLayer("Player"), true);
            Physics.IgnoreLayerCollision(HidingPhysicsGroups.SecondLayer, LayerMask.NameToLayer("Player"), true);
        }
        public void BeginPair(int value)
        {
            if (pair == value || merged) return;
            if(pair<0)
                foreach(var item in items.Values)
                    if(item!=null && item.TryGetComponent<Rigidbody>(out var body) && originalMotion.TryAdd(body,body.isKinematic))
                        body.isKinematic=true;
            ResetPair(); pair = value;
        }
        public bool TryBaselinePose(string sourceId, out Pose pose)
        {
            if (sourceId != null && splits.TryGetValue(sourceId, out var split))
            {
                pose = split.Pose;
                return true;
            }
            pose = default;
            return false;
        }
        public bool ShowPrivate(MatchObjectStateSnapshot state)
        {
            if (merged || string.IsNullOrEmpty(state.SourceObjectId)) return false;
            if (items.TryGetValue(state.ObjectId, out var current) && current != null) return true;
            if (!items.TryGetValue(state.SourceObjectId, out var source) || source == null) return false;
            if (!splits.ContainsKey(state.SourceObjectId))
                splits.Add(state.SourceObjectId, new Split { SourceId=state.SourceObjectId, Original=source,
                    Pose=new Pose(source.transform.position,source.transform.rotation), Layer=source.gameObject.layer,
                    OriginalAlias=state.ObjectId });
            source.UseObjectId(state.ObjectId);
            source.SetInteractionLayer(HidingPhysicsGroups.LayerFor(state.PrivateOwner));
            items[state.ObjectId]=source;
            return true;
        }
        public void Rollback(string id)
        {
            foreach (var split in splits.Values)
            {
                if (split.OriginalAlias == id)
                {
                    items.Remove(id); split.OriginalAlias=null;
                    split.Original.UseObjectId(split.SourceId); split.Original.OnSettled(split.Pose,false);
                    return;
                }
                if (split.DuplicateAlias == id && split.OriginalAlias == null)
                {
                    items.Remove(id);
                    split.Duplicate.gameObject.SetActive(false); DestroyBody(split.Duplicate.gameObject);
                    split.Original.SetInteractionLayer(split.Layer);
                    splits.Remove(split.SourceId);
                    return;
                }
            }
        }
        public bool Materialize(PrivateHidingWorlds.Copy copy)
        {
            if (merged) return false;
            BeginPair(copy.PlayerIndex / 2);
            if (items.TryGetValue(copy.ObjectId, out var existing) && existing != null) return true;
            if (!items.TryGetValue(copy.SourceId, out var source) || source == null) return false;
            if (validator != null && !validator.RegisterPrivateCopy(copy.ObjectId, copy.SourceId,
                HidingPhysicsGroups.LayerFor(copy.PlayerIndex))) return false;
            if (!splits.TryGetValue(copy.SourceId, out var split))
            {
                split=new Split {SourceId=copy.SourceId, Original=source,
                    Pose=new Pose(source.transform.position,source.transform.rotation), Layer=source.gameObject.layer,
                    FirstPlayer=copy.PlayerIndex};
                // Capture authored visibility before a host baseline or avatar hides a renderer.
                ShowBody(source,true);
                split.Duplicate=Clone(source,copy.ObjectId);
                ShowBody(split.Duplicate,true);
                split.DuplicateAlias=copy.ObjectId;
                source.SetInteractionLayer(HidingPhysicsGroups.LayerFor(copy.PlayerIndex ^ 1));
                splits.Add(copy.SourceId,split);
                existing=split.Duplicate;
            }
            else
            {
                // The other slot reuses the original body, which has remained at the baseline pose.
                existing=split.Original; split.OriginalAlias=copy.ObjectId;
                existing.UseObjectId(copy.ObjectId);
            }
            existing.SetInteractionLayer(HidingPhysicsGroups.LayerFor(copy.PlayerIndex));
            existing.OnSettled(copy.Pose,false);
            items[copy.ObjectId]=existing;
            return true;
        }
        private CarryableItem Clone(CarryableItem source,string id)
        {
            var go=UnityEngine.Object.Instantiate(source.gameObject);
            go.name="[Private] "+id;
            go.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(go,scene);
            var item=go.GetComponent<CarryableItem>(); item.SetOwningScene(scene); item.UseObjectId(id);
            go.SetActive(true);
            return item;
        }
        public void ResetPair()
        {
            foreach(var split in splits.Values)
            {
                split.BaselineView?.Dispose();
                ShowBody(split.Original,true); ShowBody(split.Duplicate,true);
                if(split.OriginalAlias!=null)items.Remove(split.OriginalAlias);
                if(split.DuplicateAlias!=null)items.Remove(split.DuplicateAlias);
                if(split.Duplicate!=null){split.Duplicate.gameObject.SetActive(false);DestroyBody(split.Duplicate.gameObject);}
                if(split.Original!=null)
                {
                    split.Original.UseObjectId(split.SourceId);
                    split.Original.SetInteractionLayer(split.Layer);
                    split.Original.OnSettled(split.Pose,false);
                }
            }
            splits.Clear(); renderers.Clear(); visibility.Clear();
        }
        public bool Merge(IReadOnlyList<MatchObjectStateSnapshot> states)
        {
            if(merged)return false;
            ResetPair(); merged=true; validator?.ClearPrivateLayers();
            foreach(var pair in originalMotion)if(pair.Key!=null){pair.Key.isKinematic=pair.Value;if(!pair.Value)pair.Key.Sleep();}
            originalMotion.Clear();
            var used=new HashSet<string>(StringComparer.Ordinal);
            foreach(var state in states)
            {
                if(string.IsNullOrEmpty(state.SourceObjectId) ||
                    !items.TryGetValue(state.SourceObjectId,out var source) || source==null)continue;
                var item=used.Add(state.SourceObjectId) ? source : Clone(source,state.ObjectId);
                if(item!=source)mergedCopies.Add(item);
                item.UseObjectId(state.ObjectId);
                item.SetInteractionLayer(LayerMask.NameToLayer("Carryable"));
                item.gameObject.SetActive(true);
                item.OnSettled(state.Pose,false);
                items[state.ObjectId]=item;
                validator?.RegisterPrivateCopy(state.ObjectId,state.SourceObjectId,item.gameObject.layer);
            }
            return true;
        }
        private static void DestroyBody(GameObject value)
        {
            if(Application.isPlaying)UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
        public void Dispose()
        {
            if(!merged)ResetPair();
            foreach(var pair in originalMotion)if(pair.Key!=null){pair.Key.isKinematic=pair.Value;if(!pair.Value)pair.Key.Sleep();}
            originalMotion.Clear();
            foreach(var item in mergedCopies)if(item!=null)DestroyBody(item.gameObject);
            mergedCopies.Clear();
        }
    }
}
