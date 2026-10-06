using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HabitatShift.Core;
using UnityEditor;
using UnityEngine;

public static class L31Round2Probe
{
    [Serializable] class Plan { public bool requireWin; public Step[] steps; }
    [Serializable] class Step { public string habitatId; public IntPair[] anchors; }
    static FloatPair P(Vector2 v) { return new FloatPair { x = v.x, y = v.y }; }
    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, @".docs\Verification\M12_L31_L34\l31_interlock_preview_v5.json")))
                return dir.FullName;
        throw new DirectoryNotFoundException("L31 Round 2 preview not found.");
    }
    public static void Run()
    {
        try
        {
            var root = FindRoot();
            var dir = Path.Combine(root, @".docs\Verification\M12_L31_L34");
            var preview = Path.Combine(dir, "l31_interlock_preview_v5.json");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(dir, "l31_round2_plan.json")));
            RulesetDto rules;
            var catalog = CatalogLoader.DeserializeValidatedPayloads(File.ReadAllBytes(preview),
                File.ReadAllBytes(Path.Combine(root, @"Assets\StreamingAssets\HabitatShift\ruleset_manifest_v3.json")), out rules);
            var level = catalog.levels[30];
            var findings = LevelV5Audit.Check(level);
            foreach (var f in findings) Debug.LogError("R2_AUDIT " + f.code + " " + f.habitatId + " " + f.targetId + " " + f.detail);
            Debug.Log("R2_AUDIT_RESULT findings=" + findings.Count);
            var s = new ContinuousSession(level, rules);
            var gestures = new List<TraceGestureV1>();
            foreach (var step in plan.steps)
            {
                var h = s.Committed.habitats.FirstOrDefault(x => x.id == step.habitatId);
                if (h == null) { Debug.LogError("R2_MISSING " + step.habitatId); break; }
                var press = h.anchor + (Vector2)h.shape[0] + new Vector2(.5f,.5f);
                if (!s.BeginDrag(h.id, press)) throw new Exception("Begin " + h.id);
                var path = new List<FloatPair>();
                var before = s.Committed.sprouts.Count;
                var initialStage = h.stageIndex;
                foreach (var a in step.anchors)
                {
                    var pointer = new Vector2(a.x, a.y) + (Vector2)h.shape[0] + new Vector2(.5f,.5f);
                    path.Add(P(pointer));
                    s.MovePointer(pointer);
                    Debug.Log("R2_MOVE " + h.id + " desired=" + a.x + "," + a.y + " resolved=" + s.ResolvedPose +
                        " sprouts=" + s.Preview.sprouts.Count + " habitat=" + s.Preview.habitats.Count);
                }
                var changed = s.EndDrag();
                var after = s.Committed.habitats.FirstOrDefault(x => x.id == h.id);
                Debug.Log("R2_DRAG " + h.id + " changed=" + changed + " captured=" + (before - s.Committed.sprouts.Count) +
                    " anchor=" + (after == null ? "completed" : after.anchor.ToString()) +
                    " stage=" + initialStage + "->" + (after == null ? -1 : after.stageIndex) +
                    " habitats=" + s.Committed.habitats.Count + " sprouts=" + s.Committed.sprouts.Count);
                if (changed) gestures.Add(new TraceGestureV1 { habitatId=h.id, press=P(press),
                    boardPath=path.ToArray(), checkpoint=SolutionTraceRunner.Fingerprint(s.Committed) });
            }
            Debug.Log("R2_FINAL won=" + s.Committed.won + " habitats=" + s.Committed.habitats.Count +
                " sprouts=" + s.Committed.sprouts.Count + " queues=" + string.Join(",", s.Committed.elevators.Select(x=>x.nextIndex)));
            var trace = new SolutionTraceV1 { levelId = 31, catalogFingerprint = catalog.canonicalLevelsFingerprint, gestures = gestures.ToArray() };
            File.WriteAllText(Path.Combine(dir, "l31_round2_probe_trace.json"), JsonUtility.ToJson(trace, true) + "\n");
            if (plan.requireWin && !s.Committed.won) throw new Exception("Plan did not win");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogError("R2_FAIL " + e); EditorApplication.Exit(1); }
    }
}
