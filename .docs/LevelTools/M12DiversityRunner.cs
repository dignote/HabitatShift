using System;
using System.Linq;
using HabitatShift.Core;
using HabitatShift.Tests;
using NUnit.Framework;
using UnityEditor;
public static class M12DiversityRunner
{
    public static void Run()
    {
        try
        {
            var passed = 0;
            foreach (var type in new[] { typeof(GameRulesTests), typeof(LevelSystemV5Tests) })
            foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0).OrderBy(m => m.Name))
            { method.Invoke(Activator.CreateInstance(type), null); passed++; }
            RulesetDto rules; var catalog = CatalogLoader.Load(out rules);
            for (var id = 31; id <= 35; id++)
            {
                var findings = LevelV5Audit.Check(catalog.levels[id - 1]);
                if (findings.Count != 0) throw new Exception("L" + id + " audit: " + string.Join(",", findings.Select(x => x.code).ToArray()));
            }
            UnityEngine.Debug.Log("M12_DIVERSITY_TEST_RESULT passed=" + passed + " failed=0");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { UnityEngine.Debug.LogError("M12_DIVERSITY_FAIL " + (e.InnerException ?? e)); EditorApplication.Exit(1); }
    }
}
