using System;
using System.Collections.Generic;
using Game.Server.Items;
using NUnit.Framework;
using UnityEngine;
namespace Game.Tests.EditMode
{
 public sealed class PrivateHidingWorldsTests
 {
  private static WorldObjectState[] Sources(int count=40)
  {
   var sources=new WorldObjectState[count];
   for(int i=0;i<count;i++) sources[i]=new WorldObjectState("prop-"+i,new Pose(new Vector3(i,0,0),Quaternion.identity));
   return sources;
  }
  [Test] public void SameSource_TwoOwners_KeepIndependentPoseAndOneResultEach()
  {
   var w=new PrivateHidingWorlds(2,Sources());
   Assert.That(w.TryReserve(0,"prop-0",out var a,out var created),Is.True);Assert.That(created,Is.True);
   Assert.That(w.TryReserve(1,"prop-0",out var b,out _),Is.True);Assert.That(a.ObjectId,Is.Not.EqualTo(b.ObjectId));
   Assert.That(w.TryRecordPose(0,a.ObjectId,new Pose(Vector3.left,Quaternion.identity)),Is.True);
   Assert.That(w.TryRecordPose(0,b.ObjectId,new Pose(Vector3.up,Quaternion.identity)),Is.False);
   Assert.That(w.TryGetCopy(b.ObjectId,out var unchanged),Is.True);Assert.That(unchanged.Pose.position,Is.EqualTo(Vector3.zero));
   w.Complete(0);Assert.That(w.TryPublish(out _),Is.False);w.Complete(1);
   Assert.That(w.TryPublish(out var results),Is.True);Assert.That(results.Length,Is.EqualTo(2));
   Assert.That(w.TryPublish(out _),Is.False);Assert.That(w.TryRecordPose(0,a.ObjectId,Pose.identity),Is.False);
  }
  [Test] public void RepeatedAccessAndBudget_AreBoundedAndExistingItemsRemainAvailable()
  {
   var w=new PrivateHidingWorlds(6,Sources());
   for(int p=0;p<6;p++) for(int i=0;i<32;i++) Assert.That(w.TryReserve(p,"prop-"+i,out _,out _),Is.True);
   for(int i=0;i<1000;i++) Assert.That(w.TryReserve(0,"prop-0",out _,out var created)&&!created,Is.True);
   Assert.That(w.Count,Is.EqualTo(192));Assert.That(w.TryReserve(0,"prop-32",out _,out _),Is.False);
  }
  [Test] public void FailedMaterialization_RollbackFreesOnlyOwnersReservation()
  {
   var w=new PrivateHidingWorlds(2,Sources());w.TryReserve(0,"prop-0",out var a,out _);
   Assert.That(w.RollbackReservation(1,a.ObjectId),Is.False);Assert.That(w.RollbackReservation(0,a.ObjectId),Is.True);
   Assert.That(w.Count,Is.Zero);Assert.That(w.TryReserve(0,"prop-0",out var b,out var created),Is.True);
   Assert.That(created,Is.True);Assert.That(b.ObjectId,Is.EqualTo(a.ObjectId));
  }
  [Test] public void CompletedAndInvalidRequests_DoNotChangeState()
  {
   var w=new PrivateHidingWorlds(2,Sources());w.TryReserve(0,"prop-0",out var a,out _);
   Assert.That(w.TryRecordPose(0,a.ObjectId,new Pose(new Vector3(float.NaN,0,0),Quaternion.identity)),Is.False);
   Assert.That(w.TryReserve(-1,"prop-0",out _,out _),Is.False);Assert.That(w.TryReserve(0,"missing",out _,out _),Is.False);
   w.Complete(0);Assert.That(w.TryReserve(0,"prop-0",out _,out _),Is.False);
   Assert.That(w.RollbackReservation(0,a.ObjectId),Is.False);Assert.That(w.TryRecordPose(0,a.ObjectId,Pose.identity),Is.False);
  }
  [Test] public void StableShortIds_DoNotDependOnInputOrderOrSourceLength()
  {
   var source=Sources();var reversed=(WorldObjectState[])source.Clone();Array.Reverse(reversed);
   var a=new PrivateHidingWorlds(2,source);var b=new PrivateHidingWorlds(2,reversed);
   a.TryReserve(0,"prop-0",out var x,out _);b.TryReserve(0,"prop-0",out var y,out _);
   Assert.That(x.ObjectId,Is.EqualTo(y.ObjectId));Assert.That(x.ObjectId.Length,Is.LessThanOrEqualTo(16));
   Assert.Throws<ArgumentException>(()=>new PrivateHidingWorlds(2,new[]{new WorldObjectState("~000000000",Pose.identity)}));
  }
 }
}
