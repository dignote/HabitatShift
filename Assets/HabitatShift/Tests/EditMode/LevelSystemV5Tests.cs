#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using HabitatShift.Core;
using HabitatShift.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace HabitatShift.Tests
{
    public sealed class LevelSystemV5Tests
    {
        static RulesetDto Rules(string version = "continuous-core-v3") => new RulesetDto {
            rulesetVersion = version, dragSweepStep = .08f, collectionTolerance = .12f,
            foreignSproutlingCollisionRadius = .18f, narrowCorridorDragCollisionInset = .1f,
            activeDragHabitatCollisionSkin = .04f, strictProjectionRadius = .12f,
            contactReleaseHysteresis = .20f, strictCorrectionPerSweep = .02f,
            contactSolverIterations = 4
        };
        static LevelDto Level(bool dual = true) => new LevelDto {
            id = 1, cols = 4, rows = 3, title = "Synthetic Dual Ring",
            habitats = new[] { new HabitatDto { id = "H", anchor = new IntPair { x = 0, y = 0 },
                shape = new[] { new IntPair { x = 0, y = 0 } }, movementConstraint = "FREE",
                stages = dual ? new[] { new HabitatStageDto { color = "green", need = 1 },
                                       new HabitatStageDto { color = "red", need = 1 } } : null,
                color = dual ? null : "green", need = dual ? 0 : 1 } },
            targets = dual ? new[] { new TargetDto { id = "G", color = "green", position = new FloatPair { x = 1.5f, y = .5f } },
                                     new TargetDto { id = "R", color = "red", position = new FloatPair { x = 2.5f, y = .5f } } }
                           : new[] { new TargetDto { id = "G", color = "green", position = new FloatPair { x = 1.5f, y = .5f } } },
            elevators = new ElevatorDto[0], obstacles = new IntPair[0]
        };
        static CatalogDto Catalog(bool dual = true) => new CatalogDto {
            schema = "habitat-shift-levels-v5", rulesetVersion = "continuous-core-v3",
            catalogRevision = "m11-fixture", levelCount = 1, levels = new[] { Level(dual) }
        };
        static byte[] Bytes(object value) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(value));

        [Test] public void ProductionV5_Levels31To35LoadAuditAndKeepLegacyLevels()
        {
            RulesetDto rules;
            var catalog = CatalogLoader.Load(out rules);
            Assert.AreEqual("habitat-shift-levels-v5", catalog.schema);
            Assert.AreEqual("continuous-core-v3", rules.rulesetVersion);
            Assert.AreEqual(35, catalog.levelCount);
            var v4 = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "HabitatShift", "approved_levels_v4.json"));
            var v2 = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "HabitatShift", "ruleset_manifest_v1.json"));
            RulesetDto legacyRules;
            var legacy = CatalogLoader.DeserializeValidatedPayloads(v4, v2, out legacyRules);
            Assert.AreEqual(30, legacy.levelCount);
            for (var i = 0; i < 30; i++)
                Assert.AreEqual(JsonUtility.ToJson(legacy.levels[i]), JsonUtility.ToJson(catalog.levels[i]), "Legacy level " + (i + 1));
            for (var i = 30; i < 35; i++)
            {
                var level = catalog.levels[i];
                Assert.AreEqual(i + 1, level.id);
                Assert.AreEqual(level.id == 35 ? 9 : 8, level.habitats.Length);
                Assert.AreEqual((level.id == 33 || level.id == 35) ? 3 : 2, level.habitats.Count(h => h.stages != null && h.stages.Length == 2));
                Assert.AreEqual(0, LevelV5Audit.Check(level).Count, "L" + level.id);
            }
        }

        [Test] public void ProductionV5_MigratedV2SnapshotRetainsProgressAndCanRestore()
        {
            var legacy = new SaveV2 { highest = 30, last = 29, completed = new System.Collections.Generic.List<int> { 1, 28, 30 },
                snapshotLevel = 29, snapshot = ContinuousSession.Initial(Level(false)) };
            legacy.snapshot.levelId = 29;
            legacy.snapshot.habitats[0].stages = null;
            var migrated = SaveV3Migration.FromV2(legacy);
            migrated = SaveV3Migration.WithSnapshot(migrated, migrated.snapshot, 29, "habitat-shift-levels-v5", "continuous-core-v3");
            var restored = SaveV3Migration.Restore(JsonUtility.FromJson<SaveV3>(JsonUtility.ToJson(migrated)),
                "habitat-shift-levels-v5", "continuous-core-v3");
            Assert.AreEqual(29, restored.levelId);
            Assert.AreEqual(1, restored.habitats[0].stages.Count);
            Assert.AreEqual(31, migrated.highest);
            CollectionAssert.AreEqual(new[] { 1, 28, 30 }, migrated.completed);
            Assert.IsNull(legacy.snapshot.habitats[0].stages);
        }

        [Test] public void SaveV3_L31ReworkInvalidatesOnlyOldL31Snapshot()
        {
            const string oldFingerprint = "45ecef03837600fb05088f81f0f4ae8be3da482cbf2ef058d053994d755a6605";
            const string newFingerprint = "b342b1df75ccf759188dc939131bce83655a2f9b434a8e6275076cdde36b42d7";
            var state = ContinuousSession.Initial(Level(false));
            state.levelId = 31;
            var oldL31 = SaveV3Migration.WithSnapshot(new SaveV3(), state, 31,
                "habitat-shift-levels-v5", "continuous-core-v3", oldFingerprint);
            var changed = SaveV3Migration.ForCatalog(oldL31, "habitat-shift-levels-v5",
                "continuous-core-v3", newFingerprint, oldFingerprint, 31);
            Assert.IsNull(changed.snapshot);
            Assert.AreEqual(0, changed.snapshotLevel);
            Assert.AreEqual(31, oldL31.snapshotLevel);
            state.levelId = 32;
            var oldL32 = SaveV3Migration.WithSnapshot(new SaveV3(), state, 32,
                "habitat-shift-levels-v5", "continuous-core-v3", oldFingerprint);
            var preserved = SaveV3Migration.ForCatalog(oldL32, "habitat-shift-levels-v5",
                "continuous-core-v3", newFingerprint, oldFingerprint, 31);
            Assert.AreEqual(32, preserved.snapshotLevel);
            Assert.AreEqual(newFingerprint, preserved.snapshotCatalogFingerprint);
            Assert.AreEqual(32, SaveV3Migration.Restore(preserved, "habitat-shift-levels-v5", "continuous-core-v3").levelId);
        }

        [Test] public void SaveV3_L31Round2PreservesUnchangedLevelSnapshots()
        {
            const string firstFingerprint = "45ecef03837600fb05088f81f0f4ae8be3da482cbf2ef058d053994d755a6605";
            const string previousFingerprint = "b342b1df75ccf759188dc939131bce83655a2f9b434a8e6275076cdde36b42d7";
            const string currentFingerprint = "b8aaf86c2e2f538d0a337a0a2a3ce55a306e959014d70a1fc51151bee59b6525";
            foreach (var fingerprint in new[] { firstFingerprint, previousFingerprint })
            {
                var state = ContinuousSession.Initial(Level(false));
                state.levelId = 31;
                var oldL31 = SaveV3Migration.WithSnapshot(new SaveV3(), state, 31,
                    "habitat-shift-levels-v5", "continuous-core-v3", fingerprint);
                var invalidated = SaveV3Migration.ForCatalog(oldL31, "habitat-shift-levels-v5",
                    "continuous-core-v3", currentFingerprint, previousFingerprint, 31, firstFingerprint);
                Assert.IsNull(invalidated.snapshot);
                state.levelId = 32;
                var oldL32 = SaveV3Migration.WithSnapshot(new SaveV3(), state, 32,
                    "habitat-shift-levels-v5", "continuous-core-v3", fingerprint);
                var preserved = SaveV3Migration.ForCatalog(oldL32, "habitat-shift-levels-v5",
                    "continuous-core-v3", currentFingerprint, previousFingerprint, 31, firstFingerprint);
                Assert.AreEqual(32, preserved.snapshotLevel);
                Assert.AreEqual(currentFingerprint, preserved.snapshotCatalogFingerprint);
            }
        }

        [Test] public void SaveV3_WaveAInvalidatesL31ToL33AndPreservesOtherSnapshots()
        {
            const string previous = "b8aaf86c2e2f538d0a337a0a2a3ce55a306e959014d70a1fc51151bee59b6525";
            RulesetDto rules;
            var current = CatalogLoader.Load(out rules).canonicalLevelsFingerprint;
            foreach (var level in new[] { 1, 30, 31, 32, 33, 34 })
            {
                var state = ContinuousSession.Initial(Level(false));
                state.levelId = level;
                var old = SaveV3Migration.WithSnapshot(new SaveV3(), state, level,
                    "habitat-shift-levels-v5", "continuous-core-v3", previous);
                var migrated = SaveV3Migration.ForCatalogChangedLevels(old, "habitat-shift-levels-v5",
                    "continuous-core-v3", current, 34, new[] { 31, 32, 33 }, previous);
                if (level >= 31 && level <= 33) Assert.IsNull(migrated.snapshot, "L" + level);
                else
                {
                    Assert.AreEqual(level, migrated.snapshotLevel);
                    Assert.AreEqual(current, migrated.snapshotCatalogFingerprint);
                }
                Assert.AreEqual(level, old.snapshotLevel);
            }
        }

        [Test] public void SaveV3_DiversityCorrectionPreservesL31AndInvalidatesL32ToL35()
        {
            const string waveA = "686dc2074e2a751035fa48945f99444cade396237746f2217516fe3077839172";
            const string waveB = "wave-b-catalog-fingerprint";
            foreach (var level in new[] { 31, 32, 33, 34, 35 })
            {
                var state = ContinuousSession.Initial(Level(false));
                state.levelId = level;
                var old = SaveV3Migration.WithSnapshot(new SaveV3 { highest = 34, last = 34,
                    completed = new System.Collections.Generic.List<int> { 1, 31, 33, 34 } }, state, level,
                    "habitat-shift-levels-v5", "continuous-core-v3", waveA);
                var migrated = SaveV3Migration.ForCatalogChangedLevels(old, "habitat-shift-levels-v5",
                    "continuous-core-v3", waveB, 35, new[] { 32, 33, 34, 35 }, waveA);
                Assert.AreEqual(34, migrated.highest);
                Assert.AreEqual(34, migrated.last);
                CollectionAssert.AreEqual(old.completed, migrated.completed);
                if (level >= 32) Assert.IsNull(migrated.snapshot, "L" + level);
                else Assert.AreEqual(31, SaveV3Migration.Restore(migrated,
                    "habitat-shift-levels-v5", "continuous-core-v3").levelId);
            }
            var l35 = ContinuousSession.Initial(Level(false));
            l35.levelId = 35;
            var saved = SaveV3Migration.WithSnapshot(new SaveV3(), l35, 35,
                "habitat-shift-levels-v5", "continuous-core-v3", waveB);
            var restored = SaveV3Migration.ForCatalogChangedLevels(saved, "habitat-shift-levels-v5",
                "continuous-core-v3", waveB, 35, new[] { 32, 33, 34, 35 }, waveA);
            Assert.AreEqual(35, SaveV3Migration.Restore(restored,
                "habitat-shift-levels-v5", "continuous-core-v3").levelId);
        }

        [Test] public void Versions_OnlyV4V2AndV5V3PairsPass()
        {
            CatalogLoader.Validate(Catalog(), Rules());
            RulesetDto parsedRules;
            Assert.AreEqual("habitat-shift-levels-v5", CatalogLoader.DeserializeValidatedPayloads(Bytes(Catalog()), Bytes(Rules()), out parsedRules).schema);
            var c = Catalog(); Assert.Throws<InvalidOperationException>(() => CatalogLoader.Validate(c, Rules("continuous-core-v2")));
            c.schema = "habitat-shift-levels-v4"; c.rulesetVersion = "continuous-core-v2";
            Assert.Throws<InvalidOperationException>(() => CatalogLoader.Validate(c, Rules("continuous-core-v2")));
            c.levels[0].habitats[0].stages = null; c.levels[0].habitats[0].color = "green"; c.levels[0].habitats[0].need = 1;
            c.levelCount = 30; c.catalogRevision = "m8-20261001-levels-01-18-progression-v2";
            c.levels = Enumerable.Range(1, 30).Select(i => { var l = Level(false); l.id = i; return l; }).ToArray();
            CatalogLoader.Validate(c, Rules("continuous-core-v2"));
            Assert.Throws<InvalidOperationException>(() => CatalogLoader.Validate(c, Rules()));
        }

        [Test] public void Versions_V4ExplicitEmptyStagesRejectedFromRawJson()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "HabitatShift", "approved_levels_v4.json");
            var raw = File.ReadAllText(path);
            var at = raw.IndexOf("\"habitats\"", StringComparison.Ordinal);
            var start = raw.IndexOf('{', at);
            var injected = raw.Insert(start + 1, "\"stages\": [],");
            var rules = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "HabitatShift", "ruleset_manifest_v1.json"));
            Assert.Throws<InvalidOperationException>(() => { RulesetDto parsed; CatalogLoader.DeserializeValidatedPayloads(Encoding.UTF8.GetBytes(injected), rules, out parsed); });
        }

        [Test] public void Versions_V5DualWithV2RulesRejectedFromRawJson()
        {
            var c = Catalog(); var r = Rules("continuous-core-v2");
            Assert.Throws<InvalidOperationException>(() => { RulesetDto parsed; CatalogLoader.DeserializeValidatedPayloads(Bytes(c), Bytes(r), out parsed); });
        }

        [Test] public void Stages_RejectInvalidCountsColorsAndLegacyConflict()
        {
            var c = Catalog(); var h = c.levels[0].habitats[0];
            h.stages[0].need = 0; Assert.Throws<InvalidOperationException>(() => CatalogLoader.Validate(c, Rules()));
            h.stages[0].need = 1; h.stages[1].color = "green"; Assert.Throws<InvalidOperationException>(() => CatalogLoader.Validate(c, Rules()));
            h.stages[1].color = "red"; h.color = "blue"; Assert.Throws<InvalidOperationException>(() => CatalogLoader.Validate(c, Rules()));
            h.color = null; h.stages = new HabitatStageDto[0]; Assert.Throws<InvalidOperationException>(() => CatalogLoader.Validate(c, Rules()));
        }

        [Test] public void ProductionRecoveryTraces_L32AndL33ReplayCurrentCatalog()
        {
            RulesetDto rules; var catalog=CatalogLoader.Load(out rules);
            var root=Directory.GetParent(Application.dataPath).FullName;
            foreach(var id in new[]{32,33}) foreach(var variant in new[]{"canonical","altA","altB"})
            {
                var path=Path.Combine(root,".docs","LevelTools","fixtures","m12_L"+id+"_solution_trace_v2_"+variant+".json");
                var trace=JsonUtility.FromJson<SolutionTraceV1>(File.ReadAllText(path));
                Assert.AreEqual(id,trace.levelId,path); Assert.AreEqual(catalog.canonicalLevelsFingerprint,trace.catalogFingerprint,path);
                Assert.True(SolutionTraceRunner.Replay(trace,catalog.levels[id-1],rules,catalog.canonicalLevelsFingerprint).won,path);
            }
        }

        [Test] public void Stages_CollectionBreakThenCompleteAndUndoRestart()
        {
            var s = new ContinuousSession(Level(), Rules());
            Assert.True(s.BeginDrag("H", new Vector2(.5f, .5f)));
            s.MovePointer(new Vector2(1.5f, .5f));
            Assert.AreEqual(1, s.Preview.habitats[0].stageIndex);
            Assert.AreEqual("red", s.Preview.habitats[0].ActiveColor);
            var breaks = s.Events.Where(e => e.kind == PresentationKind.HabitatLayerBreak).ToArray();
            Assert.AreEqual(1, breaks.Length); Assert.AreEqual("green", breaks[0].color); Assert.AreEqual("red", breaks[0].nextColor);
            Assert.AreEqual(1, s.Preview.sprouts.Count);
            s.MovePointer(new Vector2(2.5f, .5f));
            Assert.AreEqual(0, s.Preview.habitats.Count);
            Assert.AreEqual(1, s.Events.Count(e => e.kind == PresentationKind.HabitatComplete));
            Assert.True(s.EndDrag()); Assert.True(s.Committed.won);
            Assert.True(s.Undo()); Assert.AreEqual(0, s.Committed.habitats[0].stageIndex);
            s.Restart(); Assert.AreEqual(0, s.Committed.habitats[0].stageIndex);
            Assert.AreEqual(1, s.Committed.habitats[0].ActiveRemaining);
        }

        [Test] public void Stages_GardenShiftAndNestBloomUseCurrentStageOnly()
        {
            var l = Level(); var s = new ContinuousSession(l, Rules());
            Assert.False(s.CanUseAssist(AssistKind.GardenShift, "H", "R", out _));
            Assert.True(s.UseAssist(AssistKind.NestBloom, "H", "", Vector2Int.zero));
            Assert.AreEqual("red", s.Committed.habitats[0].ActiveColor);
            Assert.AreEqual(1, s.Committed.sprouts.Count);
            Assert.True(s.CanUseAssist(AssistKind.GardenShift, "H", "R", out _));
            Assert.True(s.UseAssist(AssistKind.GardenShift, "H", "R", Vector2Int.zero));
            Assert.True(s.Committed.won);
        }

        [Test] public void Stages_RootTrimKeepsStageAndColor()
        {
            var level = Level(); level.habitats[0].shape = new[] { new IntPair { x = 0, y = 0 }, new IntPair { x = 0, y = 1 } };
            var s = new ContinuousSession(level, Rules());
            Assert.True(s.UseAssist(AssistKind.RootTrim, "H", "", Vector2Int.zero));
            Assert.AreEqual(0, s.Committed.habitats[0].stageIndex);
            Assert.AreEqual("green", s.Committed.habitats[0].ActiveColor);
            Assert.AreEqual(1, s.Committed.habitats[0].shape.Count);
        }

        [Test] public void SaveV3_MigratesLegacyAndRoundTripsDualStageSnapshots()
        {
            var legacy = new SaveV2 { snapshot = ContinuousSession.Initial(Level(false)), snapshotLevel = 1, highest = 7 };
            legacy.snapshot.habitats[0].stages = null;
            var migrated = SaveV3Migration.FromV2(legacy);
            Assert.AreEqual(3, migrated.version); Assert.AreEqual(7, migrated.highest);
            Assert.AreEqual(1, migrated.snapshot.habitats[0].stages.Count);
            var dual = ContinuousSession.Initial(Level());
            var staged = SaveV3Migration.WithSnapshot(migrated, dual, 1, "habitat-shift-levels-v5", "continuous-core-v3");
            var roundTrip = JsonUtility.FromJson<SaveV3>(JsonUtility.ToJson(staged));
            Assert.AreEqual(0, SaveV3Migration.Restore(roundTrip, "habitat-shift-levels-v5", "continuous-core-v3").habitats[0].stageIndex);
            roundTrip.snapshot.habitats[0].stageIndex = 1;
            roundTrip.snapshot.habitats[0].stages[0].remaining = 0;
            roundTrip.snapshot.habitats[0].color = "red"; roundTrip.snapshot.habitats[0].remaining = 1;
            Assert.AreEqual(1, SaveV3Migration.Restore(roundTrip, "habitat-shift-levels-v5", "continuous-core-v3").habitats[0].stageIndex);
            Assert.Throws<InvalidOperationException>(() => SaveV3Migration.Restore(roundTrip, "habitat-shift-levels-v4", "continuous-core-v2"));
        }

        [Test] public void TraceV1_ReplaysSyntheticWinAndChecksFingerprint()
        {
            var level = Level(false); var s = new ContinuousSession(level, Rules());
            Assert.True(s.BeginDrag("H", new Vector2(.5f, .5f)));
            s.MovePointer(new Vector2(1.5f, .5f)); Assert.True(s.EndDrag()); Assert.True(s.Committed.won);
            var trace = new SolutionTraceV1 { levelId = 1, catalogFingerprint = "fixture-sha",
                gestures = new[] { new TraceGestureV1 { habitatId = "H", press = new FloatPair { x = .5f, y = .5f },
                    boardPath = new[] { new FloatPair { x = 1.5f, y = .5f } }, checkpoint = SolutionTraceRunner.Fingerprint(s.Committed) } } };
            Assert.True(SolutionTraceRunner.Replay(trace, level, Rules(), "fixture-sha").won);
            Assert.Throws<InvalidOperationException>(() => SolutionTraceRunner.Replay(trace, level, Rules(), "different-catalog"));
            trace.gestures[0].checkpoint = "wrong";
            Assert.Throws<InvalidOperationException>(() => SolutionTraceRunner.Replay(trace, level, Rules(), "fixture-sha"));
        }

        [Test] public void AuditV5_DetectsGreenRedQueueOrderingBlocker()
        {
            var level = Level(); level.targets = new TargetDto[0];
            level.elevators = new[] { new ElevatorDto { id = "E", direction = "RIGHT", entry = new FloatPair { x = 3.5f, y = 2.5f },
                queue = new[] { new QueueItemDto { id = "R1", color = "red" }, new QueueItemDto { id = "R2", color = "red" },
                    new QueueItemDto { id = "G1", color = "green" } } } };
            Assert.True(LevelV5Audit.Check(level).Any(x => x.code == "PROVEN_QUEUE_STAGE_BLOCKER"));
        }


        [Test] public void AuditV5_RejectsDuplicateInitialTargetCellsForAnyColors()
        {
            var level = Level(false);
            level.targets = new[] {
                new TargetDto { id = "G", color = "green", position = new FloatPair { x = 1.5f, y = .5f } },
                new TargetDto { id = "R", color = "red", position = new FloatPair { x = 1.5f, y = .5f } } };
            Assert.True(LevelV5Audit.Check(level).Any(x => x.code == "DUPLICATE_TARGET_POSITION"));
            level.targets[1].color = "green";
            Assert.True(LevelV5Audit.Check(level).Any(x => x.code == "DUPLICATE_TARGET_POSITION"));
        }

        [Test] public void AuditV5_ChecksAllStageSupplyMaskAndMobility()
        {
            var level = Level();
            Assert.False(LevelV5Audit.Check(level).Any(x => x.code == "SUPPLY" || x.code == "PLACEMENT" || x.code == "MASK"));
            level.targets = new[] { level.targets[0] };
            Assert.True(LevelV5Audit.Check(level).Any(x => x.code == "SUPPLY"));
            level.playableMask = new[] { new IntPair { x = 0, y = 0 }, new IntPair { x = 0, y = 0 } };
            Assert.True(LevelV5Audit.Check(level).Any(x => x.code == "MASK" || x.code == "MOBILITY"));
        }

        [Test] public void Presentation_DualRingIsShapeGenericAndChangesColorOnStageBreak()
        {
            var shapes = new[] { new[] { new IntPair { x = 0, y = 0 } },
                new[] { new IntPair { x = 0, y = 0 }, new IntPair { x = 1, y = 0 } },
                new[] { new IntPair { x = 0, y = 0 }, new IntPair { x = 0, y = 1 }, new IntPair { x = 1, y = 0 } },
                new[] { new IntPair { x = 0, y = 0 }, new IntPair { x = 1, y = 0 }, new IntPair { x = 0, y = 1 }, new IntPair { x = 1, y = 1 } } };
            var go = new GameObject("dual ring test");
            try
            {
                var view = go.AddComponent<HabitatPresentation>();
                foreach (var shape in shapes)
                {
                    var level = Level(); level.habitats[0].shape = shape;
                    var state = ContinuousSession.Initial(level);
                    view.Render(state, level);
                    var first = go.GetComponentsInChildren<SpriteRenderer>().First(x => x.name == "habitat/H").sprite;
                    Assert.True(first.name.StartsWith("DualRing/"));
                    state.habitats[0].stages[0].remaining = 0; state.habitats[0].stageIndex = 1;
                    state.habitats[0].NormalizeLegacy(); view.Render(state, level);
                    var second = go.GetComponentsInChildren<SpriteRenderer>().First(x => x.name == "habitat/H").sprite;
                    Assert.AreNotSame(first, second);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void Presentation_DualRingPixelsAndPreviewMatchBoardFootprint()
        {
            var shapes = new[] {
                new[] { new IntPair { x = 0, y = 0 }, new IntPair { x = 1, y = 0 }, new IntPair { x = 0, y = 1 } }, // L31 cyan: lower right is empty
                new[] { new IntPair { x = 0, y = 0 }, new IntPair { x = 0, y = 1 }, new IntPair { x = 1, y = 1 } },
                new[] { new IntPair { x = 1, y = 0 }, new IntPair { x = 0, y = 1 }, new IntPair { x = 1, y = 1 } }
            };
            foreach (var shape in shapes)
            {
                var go = new GameObject("dual ring footprint test");
                try
                {
                    var level = Level(); level.habitats[0].shape = shape;
                    level.habitats[0].anchor = new IntPair { x = 1, y = 0 };
                    var view = go.AddComponent<HabitatPresentation>();
                    view.SetSettlePreview("H", new Vector2(1, 0), true);
                    view.Render(ContinuousSession.Initial(level), level);
                    var renderers = go.GetComponentsInChildren<SpriteRenderer>();
                    var tray = renderers.First(r => r.name == "habitat/H");
                    var texture = tray.sprite.texture;
                    for (var y = 0; y < 2; y++) for (var x = 0; x < 2; x++)
                    {
                        var occupied = shape.Any(c => c.x == x && c.y == y);
                        var pixel = texture.GetPixel(16 + x * 128 + 64, 16 + (1 - y) * 128 + 64);
                        Assert.AreEqual(occupied, pixel.a > .9f, "Dual-Ring pixel at board cell " + x + "," + y);
                        var preview = renderers.FirstOrDefault(r => r.name == "preview/H/" + x + "/" + y);
                        Assert.AreEqual(occupied, preview != null, "Preview at board cell " + x + "," + y);
                        if (preview != null)
                            Assert.Less(Vector3.Distance(preview.transform.position,
                                BoardViewport.ToWorld(new Vector2(1 + x + .5f, y + .5f))), .001f);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }
    }
}
#endif
