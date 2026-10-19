using System.Collections.Generic;
using Game.Bootstrap;
using Game.Client.Interactions;
using Game.Core.Match;
using Game.Network.Match;
using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public class SparseHidingObjectsTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SameSource_HasIndependentBodies_ResetsPair_AndMergesOverlapsFrozen(int viewer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "private-test-source";
            var item = go.AddComponent<CarryableItem>(); item.UseObjectId("source");
            item.SetInteractionLayer(LayerMask.NameToLayer("Carryable"));
            var items = new Dictionary<string, CarryableItem> { ["source"] = item };
            var pose = new Pose(new Vector3(30, 1, 30), Quaternion.identity);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var world = new PrivateHidingWorlds(4, new[] { new WorldObjectState("source", pose) });
            using var sparse = new SparseHidingObjects(items, go.scene, null);
            try
            {
                world.TryReserve(0, "source", out var a, out _);
                world.TryReserve(1, "source", out var b, out _);
                Assert.That(sparse.Materialize(a), Is.True);
                Assert.That(sparse.Materialize(b), Is.True);
                Assert.That(sparse.TryBaselinePose("source", out var baseline), Is.True);
                Assert.That(baseline.position, Is.EqualTo(pose.position));
                Assert.That(items[a.ObjectId], Is.Not.SameAs(items[b.ObjectId]));
                Assert.That(Physics.GetIgnoreLayerCollision(HidingPhysicsGroups.FirstLayer, HidingPhysicsGroups.SecondLayer), Is.True);
                items[a.ObjectId].transform.position += Vector3.right * 5;
                Assert.That(items[b.ObjectId].transform.position, Is.EqualTo(pose.position));
                sparse.ShowHostView(viewer);
                Assert.That(items[a.ObjectId].GetComponent<Renderer>().forceRenderingOff, Is.EqualTo(viewer!=0));
                Assert.That(items[b.ObjectId].GetComponent<Renderer>().forceRenderingOff, Is.EqualTo(viewer!=1));
                sparse.BeginPair(1);
                Assert.That(item.ObjectId, Is.EqualTo("source"));
                Assert.That(item.GetComponent<Renderer>().forceRenderingOff,Is.False,"Host baseline visibility must restore after the pair.");
                Assert.That(item.transform.position, Is.EqualTo(pose.position));
                Assert.That(items.ContainsKey(a.ObjectId), Is.False);
                var states = new[] {
                    new MatchObjectStateSnapshot(a.ObjectId,-1,pose,Vector3.zero,false,1,sourceObjectId:"source"),
                    new MatchObjectStateSnapshot(b.ObjectId,-1,pose,Vector3.zero,false,1,sourceObjectId:"source") };
                Assert.That(sparse.Merge(states), Is.True);
                Assert.That(sparse.Merge(states), Is.False);
                Assert.That(items[a.ObjectId], Is.Not.SameAs(items[b.ObjectId]));
                foreach(var id in new[]{a.ObjectId,b.ObjectId})
                {
                    Assert.That(items[id].gameObject.activeSelf, Is.True);
                    Assert.That(items[id].GetComponent<Rigidbody>().isKinematic, Is.True);
                    Assert.That(items[id].transform.position, Is.EqualTo(pose.position));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
