using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HabitatShift.Core;
using UnityEditor;
using UnityEngine;

public static class L31InterlockProbe
{
    static readonly string Root = FindRoot();
    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, @"Assets\StreamingAssets\HabitatShift\approved_levels_v5.json")) &&
                File.Exists(Path.Combine(dir.FullName, @".docs\LevelTools\rework_l31_interlock.py")))
                return dir.FullName;
        throw new DirectoryNotFoundException("L31 repository root not found.");
    }
    static FloatPair P(float x, float y) { return new FloatPair { x = x, y = y }; }
    public static void Run()
    {
        try
        {
            RulesetDto rules;
            var catalog = CatalogLoader.Load(out rules);
            var level = catalog.levels[30];
            var findings = LevelV5Audit.Check(level);
            foreach (var f in findings) Debug.LogError("L31_AUDIT " + f.code + " " + f.habitatId + " " + f.targetId + " " + f.detail);
            Debug.Log("L31_AUDIT_RESULT findings=" + findings.Count);
            var s = new ContinuousSession(level, rules);
            var gestures = new List<TraceGestureV1>();
            Drag(s, gestures, "GATE", new[] { new Vector2(3, 4) });
            Drag(s, gestures, "YELLOW_KEY", new[] { new Vector2(4, 4) });
            Drag(s, gestures, "BLUE_SQUARE", new[] { new Vector2(2, 2), new Vector2(3, 2), new Vector2(3, 1) });
            Drag(s, gestures, "PURPLE_SQUARE", new[] { new Vector2(0, 6), new Vector2(0, 4), new Vector2(0, 3) });
            Drag(s, gestures, "PINK_RING", new[] { new Vector2(2, 6), new Vector2(1, 6), new Vector2(0, 6), new Vector2(2, 6) });
            Drag(s, gestures, "PINK_RING", new[] { new Vector2(0, 6) });
            Drag(s, gestures, "ORANGE_TRIO", new[] { new Vector2(0, 0), new Vector2(4, 0) });
            Drag(s, gestures, "YELLOW_KEY", new[] { new Vector2(4, 5) });
            Drag(s, gestures, "GATE", new[] { new Vector2(2, 4), new Vector2(2, 3), new Vector2(2, 2), new Vector2(2, 1), new Vector2(1, 1), new Vector2(2, 1), new Vector2(3, 1), new Vector2(3, 2), new Vector2(3, 3), new Vector2(3, 4), new Vector2(4, 4), new Vector2(5, 4), new Vector2(6, 4), new Vector2(7, 4) });
            Drag(s, gestures, "CYAN_RING", new[] { new Vector2(6, 5), new Vector2(6, 4) });
            Drag(s, gestures, "YELLOW_KEY", new[] { new Vector2(4, 6), new Vector2(5, 6), new Vector2(6, 6) });
            Drag(s, gestures, "CYAN_RING", new[] { new Vector2(6, 5), new Vector2(6, 6) });
            Drag(s, gestures, "GREEN_L", new[] { new Vector2(5, 1), new Vector2(6, 1), new Vector2(6, 2), new Vector2(6, 3) });
            Drag(s, gestures, "CYAN_RING", new[] { new Vector2(6, 5), new Vector2(6, 4), new Vector2(6, 3), new Vector2(6, 2), new Vector2(6, 1), new Vector2(6, 0), new Vector2(5, 0) });
            Drag(s, gestures, "CYAN_RING", new[] { new Vector2(6, 0) });
            Debug.Log("L31_FINAL won=" + s.Committed.won + " habitats=" + s.Committed.habitats.Count + " sprouts=" + s.Committed.sprouts.Count + " queues=" + string.Join(",",s.Committed.elevators.Select(x=>x.nextIndex)));
            if (!s.Committed.won) throw new Exception("L31 did not win.");
            var trace = new SolutionTraceV1 { levelId = 31, catalogFingerprint = catalog.canonicalLevelsFingerprint, gestures = gestures.ToArray() };
            File.WriteAllText(Path.Combine(Root, @".docs\Verification\M12_L31_L34\l31_probe_trace.json"), JsonUtility.ToJson(trace, true) + "\n");
            SolutionTraceRunner.Replay(trace, level, rules, catalog.canonicalLevelsFingerprint);
            Debug.Log("L31_TRACE_PASS gestures=" + gestures.Count);
            Variant("yellow-first", level, rules, catalog.canonicalLevelsFingerprint, new[] {
                "YELLOW_KEY:4,4", "GATE:3,4", "BLUE_SQUARE:2,2;3,2;3,1", "PURPLE_SQUARE:0,6;0,4;0,3",
                "PINK_RING:2,6;1,6;0,6;2,6", "PINK_RING:0,6", "ORANGE_TRIO:0,0;4,0", "YELLOW_KEY:4,5",
                "GATE:2,4;2,3;2,2;2,1;1,1;2,1;3,1;3,2;3,3;3,4;4,4;5,4;6,4;7,4",
                "CYAN_RING:6,5;6,4", "YELLOW_KEY:4,6;5,6;6,6", "CYAN_RING:6,5;6,6",
                "GREEN_L:5,1;6,1;6,2;6,3", "CYAN_RING:6,5;6,4;6,3;6,2;6,1;6,0;5,0", "CYAN_RING:6,0" });
            Variant("perimeter-first", level, rules, catalog.canonicalLevelsFingerprint, new[] {
                "ORANGE_TRIO:0,0;4,0", "GATE:3,4", "YELLOW_KEY:4,4", "BLUE_SQUARE:2,2;3,2;3,1",
                "PURPLE_SQUARE:0,6;0,4;0,3", "PINK_RING:2,6;1,6;0,6;2,6", "PINK_RING:0,6", "YELLOW_KEY:4,5",
                "GATE:2,4;2,3;2,2;2,1;1,1;2,1;3,1;3,2;3,3;3,4;4,4;5,4;6,4;7,4",
                "CYAN_RING:6,5;6,4", "YELLOW_KEY:4,6;5,6;6,6", "CYAN_RING:6,5;6,6",
                "GREEN_L:5,1;6,1;6,2;6,3", "CYAN_RING:6,5;6,4;6,3;6,2;6,1;6,0;5,0", "CYAN_RING:6,0" });
            Variant("pink-ring-first", level, rules, catalog.canonicalLevelsFingerprint, new[] {
                "PINK_RING:2,6", "GATE:3,4", "YELLOW_KEY:4,4", "BLUE_SQUARE:2,2;3,2;3,1",
                "PURPLE_SQUARE:0,6;0,4;0,3", "PINK_RING:1,6;0,6;2,6", "PINK_RING:0,6",
                "ORANGE_TRIO:0,0;4,0", "YELLOW_KEY:4,5",
                "GATE:2,4;2,3;2,2;2,1;1,1;2,1;3,1;3,2;3,3;3,4;4,4;5,4;6,4;7,4",
                "CYAN_RING:6,5;6,4", "YELLOW_KEY:4,6;5,6;6,6", "CYAN_RING:6,5;6,6",
                "GREEN_L:5,1;6,1;6,2;6,3", "CYAN_RING:6,5;6,4;6,3;6,2;6,1;6,0;5,0", "CYAN_RING:6,0" });
            Variant("purple-first", level, rules, catalog.canonicalLevelsFingerprint, new[] {
                "PURPLE_SQUARE:0,6;0,4", "GATE:3,4", "YELLOW_KEY:4,4", "BLUE_SQUARE:2,2;3,2;3,1",
                "PURPLE_SQUARE:0,3", "PINK_RING:2,6;1,6;0,6;2,6", "PINK_RING:0,6",
                "ORANGE_TRIO:0,0;4,0", "YELLOW_KEY:4,5",
                "GATE:2,4;2,3;2,2;2,1;1,1;2,1;3,1;3,2;3,3;3,4;4,4;5,4;6,4;7,4",
                "CYAN_RING:6,5;6,4", "YELLOW_KEY:4,6;5,6;6,6", "CYAN_RING:6,5;6,6",
                "GREEN_L:5,1;6,1;6,2;6,3", "CYAN_RING:6,5;6,4;6,3;6,2;6,1;6,0;5,0", "CYAN_RING:6,0" });
            Variant("cyan-ring-first", level, rules, catalog.canonicalLevelsFingerprint, new[] {
                "CYAN_RING:6,5", "GATE:3,4", "YELLOW_KEY:4,4", "BLUE_SQUARE:2,2;3,2;3,1",
                "PURPLE_SQUARE:0,6;0,4;0,3", "PINK_RING:2,6;1,6;0,6;2,6", "PINK_RING:0,6",
                "ORANGE_TRIO:0,0;4,0", "YELLOW_KEY:4,5",
                "GATE:2,4;2,3;2,2;2,1;1,1;2,1;3,1;3,2;3,3;3,4;4,4;5,4;6,4;7,4",
                "CYAN_RING:6,4", "YELLOW_KEY:4,6;5,6;6,6", "CYAN_RING:6,5;6,6",
                "GREEN_L:5,1;6,1;6,2;6,3", "CYAN_RING:6,5;6,4;6,3;6,2;6,1;6,0;5,0", "CYAN_RING:6,0" });
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogError("L31_FAIL " + e); EditorApplication.Exit(1); }
    }
    static void Variant(string name, LevelDto level, RulesetDto rules, string fingerprint, string[] specs)
    {
        var session = new ContinuousSession(level, rules);
        var gestures = new List<TraceGestureV1>();
        foreach (var spec in specs)
        {
            var halves = spec.Split(':');
            var route = halves[1].Split(';').Select(x => {
                var pair = x.Split(','); return new Vector2(int.Parse(pair[0]), int.Parse(pair[1]));
            }).ToArray();
            Drag(session, gestures, halves[0], route);
        }
        if (!session.Committed.won) throw new Exception("Alternate route did not win: " + name);
        var trace = new SolutionTraceV1 { levelId = 31, catalogFingerprint = fingerprint, gestures = gestures.ToArray() };
        SolutionTraceRunner.Replay(trace, level, rules, fingerprint);
        Debug.Log("L31_ALTERNATE_PASS " + name + " gestures=" + gestures.Count);
    }
    static void Drag(ContinuousSession s, List<TraceGestureV1> gestures, string key, Vector2[] anchors)
    {
        var id = "L31_H_" + key;
        var h = s.Committed.habitats.FirstOrDefault(x => x.id == id);
        if (h == null) throw new Exception("No Habitat " + id);
        var press = h.anchor + (Vector2)h.shape[0] + new Vector2(.5f,.5f);
        if (!s.BeginDrag(id, press)) throw new Exception("Begin failed " + id);
        var path = new List<FloatPair>();
        foreach (var anchor in anchors)
        {
            var pointer = anchor + (Vector2)h.shape[0] + new Vector2(.5f,.5f);
            path.Add(P(pointer.x,pointer.y));
            s.MovePointer(pointer);
            Debug.Log("L31_MOVE " + key + " desired=" + anchor + " resolved=" + s.ResolvedPose + " sprouts=" + s.Preview.sprouts.Count + " habitats=" + s.Preview.habitats.Count);
        }
        var before = s.Committed.sprouts.Count;
        var changed = s.EndDrag();
        Debug.Log("L31_DRAG " + key + " changed=" + changed + " captured=" + (before-s.Committed.sprouts.Count) + " habitats=" + s.Committed.habitats.Count + " sprouts=" + s.Committed.sprouts.Count);
        if (!changed) throw new Exception("End failed " + id);
        gestures.Add(new TraceGestureV1{habitatId=id,press=P(press.x,press.y),boardPath=path.ToArray(),checkpoint=SolutionTraceRunner.Fingerprint(s.Committed)});
    }
}
