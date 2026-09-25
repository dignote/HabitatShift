using NUnit.Framework;
using HabitatShift.Core;
using UnityEngine;
namespace HabitatShift.Tests {
public sealed class GameRulesTests {
  LevelDto Level(string constraint="FREE") { return new LevelDto { id=1, cols=6, rows=6, habitats=new[]{new HabitatDto{id="H",color="orange",anchor=new IntPair{x=0,y=0},shape=new[]{new IntPair{x=0,y=0}},need=2,movementConstraint=constraint}}, targets=new[]{new TargetDto{id="S1",color="orange",position=new FloatPair{x=2.5f,y=.5f}},new TargetDto{id="S2",color="orange",position=new FloatPair{x=3.5f,y=.5f}}}, elevators=new ElevatorDto[0], obstacles=new IntPair[0]}; }
  RulesetDto Rules()=>new RulesetDto{dragSweepStep=.08f,collectionTolerance=.12f,foreignSproutlingCollisionRadius=.18f,activeDragHabitatCollisionSkin=.04f,contactSolverIterations=4};
  [Test] public void ContinuousDrag_CollectsAndCommitsOneMove(){var s=new ContinuousSession(Level(),Rules());Assert.True(s.BeginDrag("H",new Vector2(.5f,.5f)));s.MovePointer(new Vector2(3.5f,.5f));Assert.True(s.EndDrag());Assert.AreEqual(1,s.Committed.moves);Assert.True(s.Committed.won);}
  [Test] public void Constraint_FiltersForbiddenAxis(){var s=new ContinuousSession(Level("HORIZONTAL_ONLY"),Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(.5f,3.5f));s.EndDrag();Assert.AreEqual(0f,s.Committed.habitats[0].anchor.y);}
  [Test] public void Undo_RestoresCommittedState(){var s=new ContinuousSession(Level(),Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(2.5f,.5f));s.EndDrag();Assert.True(s.Undo());Assert.AreEqual(0,s.Committed.moves);Assert.AreEqual(2,s.Committed.sprouts.Count);}
  [Test] public void GardenShift_ConsumesOneTarget(){var s=new ContinuousSession(Level(),Rules());Assert.True(s.UseAssist(AssistKind.GardenShift,"H","S1",Vector2Int.zero));Assert.AreEqual(1,s.Committed.sprouts.Count);}
}}
