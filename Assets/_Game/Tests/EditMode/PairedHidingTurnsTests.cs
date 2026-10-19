using Game.Core.Match;
using NUnit.Framework;
namespace Game.Tests.EditMode
{
 public sealed class PairedHidingTurnsTests
 {
  [TestCase(2,1)][TestCase(3,2)][TestCase(4,2)][TestCase(5,3)][TestCase(6,3)]
  public void TwoPerTurn_IncludesOddTail(int count,int turns)
  {
   Assert.That(HidingTurns.TurnCount(count,2),Is.EqualTo(turns));
   for(int t=0;t<turns;t++)
   {
    int first=HidingTurns.IndexAt(MatchPhase.Hiding,turns*10, t*10,count,10,2);
    Assert.That(first,Is.EqualTo(t*2));
    for(int p=0;p<count;p++)Assert.That(HidingTurns.Contains(first,p,count,2),Is.EqualTo(p/2==t));
    Assert.That(HidingTurns.RemainingSecondsAt(MatchPhase.Hiding,turns*10,t*10+4,count,10,2),Is.EqualTo(6));
   }
  }
  [Test] public void IntroAndExpiredPair_DoNotInventAPlayer()
  {
   Assert.That(HidingTurns.IndexAt(MatchPhase.Hiding,0,1,3,10,2),Is.EqualTo(-1));
   Assert.That(HidingTurns.IndexAt(MatchPhase.Hiding,23,2,3,10,2),Is.EqualTo(-1));
   Assert.That(HidingTurns.IndexAt(MatchPhase.Hiding,20,21,3,10,2),Is.EqualTo(2));
   Assert.That(HidingTurns.RemainingSecondsAt(MatchPhase.Hiding,20,21,3,10,2),Is.Zero);
   Assert.That(HidingTurns.Contains(2,3,3,2),Is.False);
  }
 }
}
