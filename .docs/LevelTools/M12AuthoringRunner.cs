using System;
using System.IO;
using System.Linq;
using HabitatShift.Core;
using HabitatShift.Runtime;
using HabitatShift.Tests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public static class M12AuthoringRunner
{
    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, @".docs\LevelTools\fixtures\m12_l31_l34_candidate_v5.json")) &&
                File.Exists(Path.Combine(dir.FullName, @"Assets\StreamingAssets\HabitatShift\approved_levels_v5.json")))
                return dir.FullName;
        throw new DirectoryNotFoundException("M12 repository root not found.");
    }

    public static void Run()
    {
        try
        {
            var root = FindRoot();
            var passed = 0;
            foreach (var type in new[] { typeof(GameRulesTests), typeof(LevelSystemV5Tests) })
            {
                var suite = Activator.CreateInstance(type);
                foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0).OrderBy(m => m.Name))
                {
                    try { method.Invoke(suite, null); passed++; }
                    catch (Exception e) { throw new Exception("Test failed: " + type.Name + "." + method.Name, e.InnerException ?? e); }
                }
            }
            Debug.Log("M12_TEST_RESULT passed=" + passed + " failed=0");

            RulesetDto productionRules;
            var production = CatalogLoader.Load(out productionRules);
            RulesetDto candidateRules;
            var fixtures = Path.Combine(root, @".docs\LevelTools\fixtures");
            var candidate = CatalogLoader.DeserializeValidatedPayloads(
                File.ReadAllBytes(Path.Combine(fixtures, "m12_l31_l34_candidate_v5.json")),
                File.ReadAllBytes(Path.Combine(fixtures, "m11_ruleset_v3.json")), out candidateRules);
            if (production.levelCount != 35 || candidate.levelCount != 35 ||
                production.canonicalLevelsFingerprint != candidate.canonicalLevelsFingerprint)
                throw new Exception("Production/candidate v5 identity differs.");
            Debug.Log("M12_CATALOG_VALID levels=35 fingerprint=" + production.canonicalLevelsFingerprint);

            for (var id = 31; id <= 35; id++)
            {
                var level = production.levels[id - 1];
                var findings = LevelV5Audit.Check(level);
                foreach (var finding in findings)
                    Debug.LogError("M12_AUDIT L" + id + " " + finding.code + " " + finding.habitatId + " " + finding.targetId + " " + finding.detail);
                if (findings.Count != 0) throw new Exception("L" + id + " has " + findings.Count + " audit findings.");
                Debug.Log("M12_AUDIT_RESULT L" + id + " findings=0");

                var cameraObject = new GameObject("M12 render camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                var viewObject = new GameObject("M12 render view");
                var view = viewObject.AddComponent<HabitatPresentation>();
                view.Initialize(camera);
                view.Render(ContinuousSession.Initial(level), level);
                Debug.Log("M12_RENDER_PASS L" + id);
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);

                var tracePath = Path.Combine(fixtures, "m12_L" + id + "_solution_trace_v1.json");
                var trace = JsonUtility.FromJson<SolutionTraceV1>(File.ReadAllText(tracePath));
                var verified = SolutionTraceRunner.Replay(trace, level, productionRules, production.canonicalLevelsFingerprint);
                var fixtureReplay = SolutionTraceRunner.Replay(trace, candidate.levels[id - 1], candidateRules, candidate.canonicalLevelsFingerprint);
                if (!verified.won || !fixtureReplay.won) throw new Exception("L" + id + " did not win.");
                Debug.Log("M12_PRODUCTION_TRACE_PASS L" + id + " gestures=" + trace.gestures.Length);
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogError("M12_FAIL " + e); EditorApplication.Exit(1); }
    }
}
