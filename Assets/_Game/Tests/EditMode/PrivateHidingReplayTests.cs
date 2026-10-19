using System;
using System.Linq;
using Game.Bootstrap;
using Game.Core.Match;
using Game.Network.Session;
using Game.Server.Items;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public class PrivateHidingReplayTests
    {
        [Test]
        public void StaticLayout_RoundTrips192ObjectsWithoutAddingThemToEveryFrame()
        {
            var layout=Enumerable.Range(0,192).Select(i=>new WorldObjectState("~"+i,new Pose(Vector3.right*i,Quaternion.identity))).ToArray();
            var segment=new HighlightSegment(10,12);
            var frames=new[]{new HighlightReplayFrame(10,new[]{Pose.identity},Array.Empty<WorldObjectState>())};
            var data=new HighlightReplayData(new HighlightCandidate(HighlightType.FirstBlood,new[]{segment},"0"),
                new[]{new HighlightReplayClip(segment,frames,layout)});
            var bytes=HighlightReplaySerializer.SerializeCompressed(new[]{data});
            Assert.That(HighlightReplaySerializer.TryDeserializeCompressed(bytes,out var decoded),Is.True);
            Assert.That(decoded[0].Clips[0].StaticObjects.Count,Is.EqualTo(192));
            Assert.That(decoded[0].Clips[0].Frames[0].WorldObjects,Is.Empty);
            Assert.That(decoded[0].Clips[0].StaticObjects[191].Pose.position,Is.EqualTo(Vector3.right*191));
        }

        [Test]
        public void StaticLayout_UsesLatestPoseBeforeClip_EvenAfterOldFramesExpire()
        {
            var buffer=new HighlightReplayBuffer(0.1,1);
            var initial=new[]{new WorldObjectState("private",Pose.identity)};
            buffer.TryRecord(1,new[]{Pose.identity},new[]{new WorldObjectState("private",new Pose(Vector3.right*3,Quaternion.identity))});
            buffer.TryRecord(3,new[]{Pose.identity},Array.Empty<WorldObjectState>());
            Assert.That(buffer.CaptureStaticObjects(initial,3)[0].Pose.position,Is.EqualTo(Vector3.right*3));
        }

        [Test]
        public void Playback_KeepsStaticPropsVisible_AndRetainsLastMovingPose()
        {
            var go=new GameObject("private replay target");
            try
            {
                var player=new HighlightReplayPlayer(Array.Empty<Transform>(),new[]{new SceneWorldObjectReference("private",go.transform)});
                var initial=new[]{new WorldObjectState("private",Pose.identity)};
                var moved=new[]{new WorldObjectState("private",new Pose(Vector3.right*4,Quaternion.identity))};
                var frames=new[]{new HighlightReplayFrame(1,Array.Empty<Pose>(),Array.Empty<WorldObjectState>()),
                    new HighlightReplayFrame(2,Array.Empty<Pose>(),moved),new HighlightReplayFrame(3,Array.Empty<Pose>(),Array.Empty<WorldObjectState>())};
                Assert.That(player.Start(new[]{new HighlightReplayClip(new HighlightSegment(1,3),frames,initial)}),Is.True);
                Assert.That(go.activeSelf,Is.True);
                player.Advance(1.5);
                Assert.That(go.activeSelf,Is.True);
                Assert.That(go.transform.position,Is.EqualTo(Vector3.right*4));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
