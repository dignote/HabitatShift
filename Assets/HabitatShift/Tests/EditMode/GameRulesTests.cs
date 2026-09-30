#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using HabitatShift.Core;
using UnityEngine;

namespace HabitatShift.Tests {
public sealed class GameRulesTests {
  LevelDto Level(string constraint="FREE") { return new LevelDto { id=1, cols=6, rows=6, habitats=new[]{new HabitatDto{id="H",color="orange",anchor=new IntPair{x=0,y=0},shape=new[]{new IntPair{x=0,y=0}},need=2,movementConstraint=constraint}}, targets=new[]{new TargetDto{id="S1",color="orange",position=new FloatPair{x=2.5f,y=.5f}},new TargetDto{id="S2",color="orange",position=new FloatPair{x=3.5f,y=.5f}}}, elevators=new ElevatorDto[0], obstacles=new IntPair[0]}; }
  RulesetDto Rules()=>new RulesetDto{dragSweepStep=.08f,collectionTolerance=.12f,foreignSproutlingCollisionRadius=.18f,narrowCorridorDragCollisionInset=.1f,activeDragHabitatCollisionSkin=.04f,strictProjectionRadius=.12f,contactReleaseHysteresis=.20f,strictCorrectionPerSweep=.02f,contactSolverIterations=4};
  static byte[] CatalogBytes()=>File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"HabitatShift","approved_levels_v4.json"));
  static byte[] RulesBytes()=>File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"HabitatShift","ruleset_manifest_v1.json"));

  [Test] public void CatalogPayload_ValidProductionBytes_DeserializeAndValidate(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);Assert.NotNull(catalog);Assert.NotNull(rules);Assert.AreEqual(30,catalog.levelCount);Assert.AreEqual("continuous-core-v2",rules.rulesetVersion);Assert.AreEqual("m8-20260929-levels-24-30",catalog.catalogRevision);Assert.AreEqual(64,catalog.canonicalLevelsFingerprint.Length);}
  [Test] public void CatalogPayload_ChangedCatalogByte_FailsSha(){var bytes=CatalogBytes();bytes[0]^=1;Assert.Throws<InvalidOperationException>(()=>{RulesetDto rules;CatalogLoader.LoadFromBytes(bytes,RulesBytes(),out rules);});}
  [Test] public void CatalogPayload_ChangedRulesByte_FailsSha(){var bytes=RulesBytes();bytes[0]^=1;Assert.Throws<InvalidOperationException>(()=>{RulesetDto rules;CatalogLoader.LoadFromBytes(CatalogBytes(),bytes,out rules);});}
  [Test] public void CatalogPayload_EmptyPayload_Fails(){Assert.Throws<InvalidOperationException>(()=>{RulesetDto rules;CatalogLoader.LoadFromBytes(Array.Empty<byte>(),RulesBytes(),out rules);});}
  [Test] public void CatalogPayload_InvalidJson_FailsWithoutFilesystem(){Assert.Throws<InvalidOperationException>(()=>{RulesetDto rules;CatalogLoader.DeserializeValidatedPayloads(Encoding.UTF8.GetBytes("{broken"),RulesBytes(),out rules);});}
  [Test] public void CatalogPayload_InvalidIdentity_FailsWithoutFilesystem(){var raw=Encoding.UTF8.GetString(CatalogBytes()).Replace("m8-20260929-levels-24-30","invalid-revision");Assert.Throws<InvalidOperationException>(()=>{RulesetDto rules;CatalogLoader.DeserializeValidatedPayloads(Encoding.UTF8.GetBytes(raw),RulesBytes(),out rules);});}
  [Test] public void CatalogPayload_ExpandedCatalog_HasLevels19To23(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);Assert.AreEqual(30,catalog.levelCount);Assert.AreEqual(30,catalog.levels.Length);for(var i=0;i<catalog.levels.Length;i++){var level=catalog.levels[i];Assert.AreEqual(i+1,level.id);Assert.IsNotNull(level.habitats);Assert.IsNotNull(level.targets);Assert.IsNotNull(level.elevators);}Assert.AreEqual(11,catalog.levels[18].habitats.Length);Assert.AreEqual(2,catalog.levels[18].elevators.Length);Assert.AreEqual(10,catalog.levels[19].habitats.Length);Assert.AreEqual(8,catalog.levels[20].habitats.Length);Assert.AreEqual(4,catalog.levels[20].elevators.Length);Assert.AreEqual(11,catalog.levels[21].habitats.Length);Assert.AreEqual(10,catalog.levels[22].habitats.Length);Assert.AreEqual(1,catalog.levels[22].elevators.Length);Assert.IsTrue(System.Array.Exists(catalog.levels[18].habitats,habitat=>habitat.color=="white"));}
  [Test] public void CatalogPayload_Levels24To30_ProtectedBaselineAndImport(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);
    // L1..L23 khong doi: id + so habitat/queue/elevator/target/mask nhu milestone truoc.
    Assert.AreEqual(23,catalog.levels[22].id);Assert.AreEqual(58,catalog.levels[22].playableMask.Length);
    Assert.AreEqual(30,catalog.levels[29].id);
    var expected=new[]{new[]{9,22,1,0},new[]{11,24,0,0},new[]{9,18,2,0},new[]{12,18,2,0},new[]{11,19,3,0},new[]{9,16,2,3},new[]{8,20,0,0}};
    for(var index=0;index<7;index++){var level=catalog.levels[23+index];Assert.AreEqual(24+index,level.id);Assert.AreEqual(expected[index][0],level.habitats.Length,"habitats L"+level.id);Assert.AreEqual(expected[index][1],level.targets.Length,"targets L"+level.id);Assert.AreEqual(expected[index][2],level.elevators.Length,"elevators L"+level.id);Assert.AreEqual(expected[index][3],(level.obstacles??new IntPair[0]).Length,"obstacles L"+level.id);Assert.IsTrue(level.cols<=10&&level.rows<=10);}}
  [Test] public void CatalogPayload_Levels24To30_NoEmptyQueueAndNoPlaceholder(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);
    for(var index=23;index<catalog.levels.Length;index++){var level=catalog.levels[index];foreach(var elevator in level.elevators){Assert.Greater(elevator.queue.Length,0,"queue L"+level.id+" "+elevator.id);Assert.AreNotEqual("E28_R8_20",elevator.id);Assert.AreEqual(0,elevator.nextIndex);Assert.IsTrue(elevator.mandatory);}}}
  [Test] public void CatalogPayload_Level29_ObstaclesAndBrownFootprint(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);var level=catalog.levels[28];
    Assert.AreEqual(3,level.obstacles.Length);
    var blocked=new System.Collections.Generic.List<string>();foreach(var cell in level.obstacles)blocked.Add(cell.x+"_"+cell.y);blocked.Sort();
    CollectionAssert.AreEqual(new[]{"2_4","3_3","4_4"},blocked);
    var brown=System.Array.Find(level.habitats,habitat=>habitat.color=="brown");Assert.IsNotNull(brown);
    Assert.AreEqual(3,brown.need);Assert.AreEqual(3,brown.shape.Length);Assert.AreEqual(4,brown.anchor.x);Assert.AreEqual(6,brown.anchor.y);
    var cells=new System.Collections.Generic.List<string>();foreach(var cell in brown.shape)cells.Add((brown.anchor.x+cell.x)+"_"+(brown.anchor.y+cell.y));cells.Sort();
    CollectionAssert.AreEqual(new[]{"4_7","5_6","5_7"},cells);}
  [Test] public void CatalogPayload_Levels24To30_QueueOrderPreserved(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);var level=catalog.levels[27];
    var elevator=System.Array.Find(level.elevators,item=>item.id=="E28_R8_17");Assert.IsNotNull(elevator);
    var colors=new System.Collections.Generic.List<string>();foreach(var item in elevator.queue)colors.Add(item.color);
    CollectionAssert.AreEqual(new[]{"purple","orange","green","pink"},colors);
    Assert.AreEqual(7.5f,elevator.entry.y,.001f);}
  [Test] public void CatalogPayload_Levels24To30_SupplyBalanceExact(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);
    for(var index=23;index<catalog.levels.Length;index++){var level=catalog.levels[index];var colors=new System.Collections.Generic.HashSet<string>();foreach(var habitat in level.habitats)colors.Add(habitat.color);
      foreach(var color in colors){var need=0;foreach(var habitat in level.habitats)if(habitat.color==color)need+=habitat.need;
        var supply=0;foreach(var target in level.targets)if(target.color==color)supply++;
        foreach(var elevator in level.elevators)foreach(var item in elevator.queue)if(item.color==color)supply++;
        Assert.AreEqual(need,supply,"supply L"+level.id+" "+color);}}}
  [Test] public void CatalogPayload_WhiteAndWalnutSpriteContract(){const System.Reflection.BindingFlags Hidden=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var presentation=typeof(HabitatShift.Runtime.HabitatPresentation);
    var family=presentation.GetMethod("Family",Hidden);var spriteName=presentation.GetMethod("SpriteName",Hidden);
    Assert.AreEqual("walnut",family.Invoke(null,new object[]{"brown"}));Assert.AreEqual("ivory",family.Invoke(null,new object[]{"white"}));
    Assert.AreEqual("sproutling_walnut_v2",spriteName.Invoke(null,new object[]{"brown"}));Assert.AreEqual("sproutling_white_v2",spriteName.Invoke(null,new object[]{"white"}));
    var fx=new GameObject("fx probe").AddComponent<HabitatShift.Runtime.GameplayFxController>();
    var sprout=fx.GetType().GetMethod("SproutSprite",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
    var walnut=(Sprite)sprout.Invoke(fx,new object[]{"brown"});var ivory=(Sprite)sprout.Invoke(fx,new object[]{"white"});
    Assert.IsNotNull(walnut);Assert.IsNotNull(ivory);Assert.AreNotSame(ivory,walnut);
    Assert.AreEqual(LoadSprite("HabitatShift/sproutling_walnut_v2"),walnut);Assert.AreEqual(LoadSprite("HabitatShift/sproutling_white_v2"),ivory);
    UnityEngine.Object.DestroyImmediate(fx.gameObject);
    Assert.AreNotEqual(GameplayFxControllerColor("brown"),GameplayFxControllerColor("pink"));}
  static Sprite LoadSprite(string path)=>Resources.Load<Sprite>(path);
  static Color GameplayFxControllerColor(string color)=>HabitatShift.Runtime.GameplayFxController.ColorFor(color);
  [Test] public void CatalogPayload_BlockerVariantsDeterministic(){const System.Reflection.BindingFlags Hidden=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
    var chooser=typeof(HabitatShift.Runtime.HabitatPresentation).GetMethod("BlockerSpriteName",Hidden);Assert.IsNotNull(chooser);
    var known=new[]{"board_blocker_stone","board_blocker_moss","board_blocker_brick"};var seen=new System.Collections.Generic.List<string>();
    for(var y=0;y<8;y++)for(var x=0;x<8;x++){var name=(string)chooser.Invoke(null,new object[]{x,y});
      Assert.Contains(name,known);Assert.AreEqual(name,chooser.Invoke(null,new object[]{x,y}),"khong on dinh tai "+x+","+y);
      if(!seen.Contains(name))seen.Add(name);Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/"+name),name);}
    Assert.AreEqual(3,seen.Count,"luoi 8x8 phai dung ca 3 bien the");
    Assert.AreEqual("board_blocker_moss",chooser.Invoke(null,new object[]{2,4}));
    Assert.AreEqual("board_blocker_stone",chooser.Invoke(null,new object[]{3,3}));
    Assert.AreEqual("board_blocker_stone",chooser.Invoke(null,new object[]{4,4}));}
  [Test] public void CatalogPayload_WalnutAndBoardAssetsExist(){Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/sproutling_walnut_v2"));Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/sproutling_white_v2"));
    Assert.IsNull(Resources.Load<Sprite>("HabitatShift/sproutling_walnut_v1"),"walnut v1 phai da bi xoa");Assert.IsNull(Resources.Load<Sprite>("HabitatShift/sproutling_ivory_v1"),"ivory v1 phai da bi xoa");
    foreach(var name in new[]{"board_blocker_stone","board_blocker_moss","board_blocker_brick"})Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/"+name),name);
    Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/tray_walnut_0_0"));Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/tray_walnut_0_0-0_1"));Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/tray_walnut_0_1-1_0-1_1"));
    for(var id=24;id<=26;id++){Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/board_mask_L"+id),"mask L"+id);Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/board_frame_L"+id),"frame L"+id);}
    for(var id=27;id<=30;id++){Assert.IsNull(Resources.Load<Sprite>("HabitatShift/Board/board_mask_L"+id),"full rect L"+id);Assert.IsNull(Resources.Load<Sprite>("HabitatShift/Board/board_frame_L"+id),"full rect L"+id);}}
  [Test] public void CatalogPayload_AllLevelHabitatTraysExist(){RulesetDto rules;var catalog=CatalogLoader.LoadFromBytes(CatalogBytes(),RulesBytes(),out rules);
    for(var index=23;index<catalog.levels.Length;index++){var level=catalog.levels[index];foreach(var habitat in level.habitats){var family=(string)typeof(HabitatShift.Runtime.HabitatPresentation).GetMethod("Family",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{habitat.color});
      var x0=int.MaxValue;var y0=int.MaxValue;foreach(var cell in habitat.shape){x0=System.Math.Min(x0,cell.x);y0=System.Math.Min(y0,cell.y);}
      var parts=new System.Collections.Generic.List<string>();foreach(var cell in habitat.shape)parts.Add((cell.x-x0)+"_"+(cell.y-y0));parts.Sort();
      var name="tray_"+family+"_"+string.Join("-",parts);Assert.IsNotNull(Resources.Load<Sprite>("HabitatShift/Board/"+name),"L"+level.id+" "+name);}}}

  [Test] public void ContinuousDrag_CollectsAndCommitsOneMove(){var s=new ContinuousSession(Level(),Rules());Assert.True(s.BeginDrag("H",new Vector2(.5f,.5f)));s.MovePointer(new Vector2(3.5f,.5f));Assert.True(s.EndDrag());Assert.AreEqual(1,s.Committed.moves);Assert.True(s.Committed.won);}
  [Test] public void Constraint_FiltersForbiddenAxis(){var s=new ContinuousSession(Level("HORIZONTAL_ONLY"),Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(.5f,3.5f));s.EndDrag();Assert.AreEqual(0f,s.Committed.habitats[0].anchor.y);}
  [Test] public void Undo_RestoresCommittedState(){var s=new ContinuousSession(Level(),Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(2.5f,.5f));s.EndDrag();Assert.True(s.Undo());Assert.AreEqual(0,s.Committed.moves);Assert.AreEqual(2,s.Committed.sprouts.Count);}
  [Test] public void GardenShift_ConsumesOneTarget(){var s=new ContinuousSession(Level(),Rules());Assert.True(s.UseAssist(AssistKind.GardenShift,"H","S1",Vector2Int.zero));Assert.AreEqual(1,s.Committed.sprouts.Count);}
  [Test] public void NestBloom_CollectsAllRequiredBoardTargets(){var s=new ContinuousSession(Level(),Rules());Assert.True(s.UseAssist(AssistKind.NestBloom,"H","",Vector2Int.zero));Assert.AreEqual(0,s.Committed.sprouts.Count);Assert.AreEqual(0,s.Committed.habitats.Count);}
  [Test] public void NestBloom_RejectsMatchingElevatorQueue(){var l=Level();l.elevators=new[]{new ElevatorDto{id="E",direction="RIGHT",entry=new FloatPair{x=5.5f,y=5.5f},queue=new[]{new QueueItemDto{id="Q",color="orange"}},nextIndex=0,mandatory=true}};var s=new ContinuousSession(l,Rules());Assert.False(s.UseAssist(AssistKind.NestBloom,"H","",Vector2Int.zero));Assert.AreEqual(2,s.Committed.sprouts.Count);}
  [Test] public void RootTrim_ReducesToOriginAndRestartRestoresShape(){var l=Level();l.habitats[0].shape=new[]{new IntPair{x=0,y=0},new IntPair{x=1,y=0}};var s=new ContinuousSession(l,Rules());Assert.True(s.UseAssist(AssistKind.RootTrim,"H","",Vector2Int.zero));CollectionAssert.AreEqual(new[]{Vector2Int.zero},s.Committed.habitats[0].shape);s.Restart();Assert.AreEqual(2,s.Committed.habitats[0].shape.Count);}
  [Test] public void RootTrim_KeepsAnchorWhenOriginCornerOccupied(){var l=Level();l.targets=new TargetDto[0];l.habitats[0].shape=new[]{new IntPair{x=0,y=0},new IntPair{x=1,y=0}};var s=new ContinuousSession(l,Rules());Assert.True(s.UseAssist(AssistKind.RootTrim,"H","",Vector2Int.zero));Assert.AreEqual(Vector2.zero,s.Committed.habitats[0].anchor);Assert.AreEqual(1,s.Committed.habitats[0].shape.Count);}
  [Test] public void RootTrim_TrimsToTopLeftOccupiedCell_WhenAnchorCornerIsEmpty(){var l=Level();l.targets=new TargetDto[0];l.habitats[0].anchor=new IntPair{x=2,y=2};l.habitats[0].shape=new[]{new IntPair{x=0,y=1},new IntPair{x=1,y=0},new IntPair{x=1,y=1}};l.habitats[0].need=2;var s=new ContinuousSession(l,Rules());Assert.True(s.CanUseAssist(AssistKind.RootTrim,"H","",out var reason));Assert.True(s.UseAssist(AssistKind.RootTrim,"H","",Vector2Int.zero));CollectionAssert.AreEqual(new[]{Vector2Int.zero},s.Committed.habitats[0].shape);Assert.AreEqual(new Vector2(3,2),s.Committed.habitats[0].anchor);}
  LevelDto WallLevel(){var l=Level();l.targets=new TargetDto[0];l.obstacles=new[]{new IntPair{x=2,y=0},new IntPair{x=2,y=1},new IntPair{x=2,y=2},new IntPair{x=2,y=3},new IntPair{x=2,y=4}};return l;}
  [Test] public void DragAgainstWall_SlidesAlongOpenTangent(){var s=new ContinuousSession(WallLevel(),Rules());Assert.True(s.BeginDrag("H",new Vector2(.5f,.5f)));s.MovePointer(new Vector2(3.5f,3.5f));Assert.LessOrEqual(s.ResolvedPose.x,1.001f);Assert.Greater(s.ResolvedPose.y,2.8f);}
  [Test] public void DragAfterWallContact_ReversesWithoutPullback(){var s=new ContinuousSession(WallLevel(),Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(3.5f,3.5f));var contacted=s.ResolvedPose.x;s.MovePointer(new Vector2(.25f,3.5f));Assert.Less(s.ResolvedPose.x,contacted-.2f);}
  [Test] public void ExactWidthCorridor_ProjectsMinorLateralDrift(){var l=Level();l.cols=3;l.rows=6;l.targets=new TargetDto[0];l.habitats[0].anchor=new IntPair{x=1,y=0};l.playableMask=new[]{new IntPair{x=1,y=0},new IntPair{x=1,y=1},new IntPair{x=1,y=2},new IntPair{x=1,y=3},new IntPair{x=1,y=4},new IntPair{x=1,y=5}};var s=new ContinuousSession(l,Rules());s.BeginDrag("H",new Vector2(1.5f,.5f));s.MovePointer(new Vector2(1.57f,4.5f));Assert.AreEqual(1f,s.ResolvedPose.x,.001f);Assert.Greater(s.ResolvedPose.y,3.8f);}
  [Test] public void NarrowGap_DoesNotPermitOverlap(){var l=Level();l.targets=new TargetDto[0];l.habitats=new[]{l.habitats[0],new HabitatDto{id="BLOCK",color="blue",anchor=new IntPair{x=2,y=0},shape=new[]{new IntPair{x=0,y=0}},need=1,movementConstraint="FREE"}};var s=new ContinuousSession(l,Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(2.49f,.5f));Assert.Less(s.ResolvedPose.x,1.06f);}
  [Test] public void Restart_ClearsDragContactAndRestoresAuthoredPose(){var s=new ContinuousSession(WallLevel(),Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(3.5f,3.5f));s.EndDrag();s.Restart();Assert.AreEqual(0f,s.Committed.habitats[0].anchor.x);Assert.AreEqual(0f,s.Committed.habitats[0].anchor.y);Assert.False(s.IsDragging);}
  [Test] public void CollectionEvent_CarriesColorAndIntakeDestination(){var s=new ContinuousSession(Level(),Rules());s.BeginDrag("H",new Vector2(.5f,.5f));s.MovePointer(new Vector2(2.5f,.5f));var e=s.Events.Find(x=>x.kind==PresentationKind.Collection);Assert.NotNull(e);Assert.AreEqual("orange",e.color);Assert.Greater(e.targetPosition.x,0f);Assert.LessOrEqual(e.targetPosition.x,2.5f);Assert.AreEqual(.5f,e.targetPosition.y,.001f);}
  [Test] public void CaptureCoverage_RequiresMoreThanTwoThirds(){var s=new ContinuousSession(Level(),Rules());var h=s.Committed.habitats[0];h.anchor=new Vector2(-.397f,0f);Assert.LessOrEqual(ContinuousSession.CaptureCoverage(h,new Vector2(.5f,.5f)),ContinuousSession.CaptureThreshold);h.anchor=new Vector2(-.395f,0f);Assert.Greater(ContinuousSession.CaptureCoverage(h,new Vector2(.5f,.5f)),ContinuousSession.CaptureThreshold);}
  [Test] public void CaptureCoverage_UsesUnionOfPolyominoCells(){var s=new ContinuousSession(Level(),Rules());var h=s.Committed.habitats[0];h.shape=new System.Collections.Generic.List<Vector2Int>{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(0,1)};h.anchor=Vector2.zero;var coverage=ContinuousSession.CaptureCoverage(h,new Vector2(.82f,.5f));Assert.Greater(coverage,0f);Assert.LessOrEqual(coverage,1f);}

  LevelDto SettleLevel(string constraint="FREE"){var level=Level(constraint);level.targets=new TargetDto[0];level.obstacles=new IntPair[0];return level;}
  ContinuousSession BeginAt(LevelDto level,Vector2 release){var session=new ContinuousSession(level,Rules());Assert.True(session.BeginDrag("H",new Vector2(.5f,.5f)));session.Preview.habitats.Find(h=>h.id=="H").anchor=release;return session;}
  static void AssertInteger(Vector2 anchor){Assert.AreEqual(Mathf.Round(anchor.x),anchor.x,.000001f);Assert.AreEqual(Mathf.Round(anchor.y),anchor.y,.000001f);}

  [Test] public void Settle_BasicNearestIntegerAnchor(){var s=BeginAt(SettleLevel(),new Vector2(2.20f,3.20f));Assert.True(s.EndDrag());var anchor=s.Committed.habitats[0].anchor;Assert.AreEqual(new Vector2(2,3),anchor);AssertInteger(anchor);}
  [Test] public void Settle_ChoosesEuclideanNearestInsteadOfLegacyScanOrder(){var level=SettleLevel();level.obstacles=new[]{new IntPair{x=2,y=3}};var s=BeginAt(level,new Vector2(2.20f,3.20f));Assert.True(s.EndDrag());Assert.AreEqual(new Vector2(3,3),s.Committed.habitats[0].anchor);}
  [Test] public void Settle_SuccessAlwaysCommitsIntegerAnchor(){var s=BeginAt(SettleLevel(),new Vector2(2.31f,3.72f));Assert.True(s.EndDrag());AssertInteger(s.Committed.habitats[0].anchor);}
  [Test] public void Settle_RejectsCandidateAllowedOnlyByActiveDragSkin(){var level=SettleLevel();level.habitats=new[]{level.habitats[0],new HabitatDto{id="OTHER",color="blue",anchor=new IntPair{x=0,y=0},shape=new[]{new IntPair{x=0,y=0}},need=1,movementConstraint="FREE"}};var s=BeginAt(level,new Vector2(.02f,0f));s.Preview.habitats.Find(h=>h.id=="OTHER").anchor=new Vector2(.98f,0f);Assert.True(s.EndDrag());Assert.AreNotEqual(Vector2.zero,s.Committed.habitats.Find(h=>h.id=="H").anchor);}
  [Test] public void Settle_RejectsObstacleAtNearestInteger(){var level=SettleLevel();level.obstacles=new[]{new IntPair{x=2,y=3}};var s=BeginAt(level,new Vector2(2.20f,3.20f));Assert.True(s.EndDrag());Assert.AreNotEqual(new Vector2(2,3),s.Committed.habitats[0].anchor);}
  [Test] public void Settle_RejectsNearestIntegerOutsidePlayableMask(){var level=SettleLevel();level.playableMask=new[]{new IntPair{x=0,y=0},new IntPair{x=3,y=3},new IntPair{x=2,y=4},new IntPair{x=3,y=4}};var s=BeginAt(level,new Vector2(2.20f,3.20f));Assert.True(s.EndDrag());Assert.AreEqual(new Vector2(3,3),s.Committed.habitats[0].anchor);}
  [Test] public void Settle_RejectsForeignSproutlingAtNearestInteger(){var level=SettleLevel();level.targets=new[]{new TargetDto{id="FOREIGN",color="blue",position=new FloatPair{x=2.5f,y=3.5f}}};var s=BeginAt(level,new Vector2(2.20f,3.20f));Assert.True(s.EndDrag());Assert.AreEqual(new Vector2(3,3),s.Committed.habitats[0].anchor);}
  [Test] public void Settle_HorizontalOnlyKeepsCommittedRow(){var level=SettleLevel("HORIZONTAL_ONLY");level.habitats[0].anchor=new IntPair{x=0,y=1};level.obstacles=new[]{new IntPair{x=4,y=1},new IntPair{x=5,y=1}};var s=BeginAt(level,new Vector2(4.20f,1.20f));Assert.True(s.EndDrag());Assert.AreEqual(1f,s.Committed.habitats[0].anchor.y);Assert.AreEqual(3f,s.Committed.habitats[0].anchor.x);}
  [Test] public void Settle_VerticalOnlyKeepsCommittedColumn(){var level=SettleLevel("VERTICAL_ONLY");level.habitats[0].anchor=new IntPair{x=1,y=0};level.obstacles=new[]{new IntPair{x=1,y=4},new IntPair{x=1,y=5}};var s=BeginAt(level,new Vector2(1.10f,4.30f));Assert.True(s.EndDrag());Assert.AreEqual(1f,s.Committed.habitats[0].anchor.x);Assert.AreEqual(3f,s.Committed.habitats[0].anchor.y);}
  [Test] public void Settle_EqualDistanceUsesLowestYThenLowestX(){Vector2 expected=new Vector2(2,3);for(var i=0;i<5;i++){var s=BeginAt(SettleLevel(),new Vector2(2.5f,3.5f));Assert.True(s.EndDrag());Assert.AreEqual(expected,s.Committed.habitats[0].anchor);}}
  [Test] public void Settle_NoStrictIntegerCandidateFailsClosed(){var level=SettleLevel();level.cols=1;level.rows=1;level.obstacles=new[]{new IntPair{x=0,y=0}};var s=BeginAt(level,new Vector2(.31f,.42f));Assert.False(s.EndDrag());Assert.AreEqual(0,s.Committed.moves);Assert.AreEqual(Vector2.zero,s.Committed.habitats[0].anchor);AssertInteger(s.Committed.habitats[0].anchor);}
  [Test] public void Settle_UndoRestoresExactPreDragAnchor(){var s=BeginAt(SettleLevel(),new Vector2(2.20f,3.20f));Assert.True(s.EndDrag());Assert.True(s.Undo());Assert.AreEqual(Vector2.zero,s.Committed.habitats[0].anchor);Assert.AreEqual(0,s.Committed.moves);}
}}
#endif
