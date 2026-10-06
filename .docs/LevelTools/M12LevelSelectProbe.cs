using System;
using System.Reflection;
using HabitatShift.Core;
using HabitatShift.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class M12LevelSelectProbe
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object instance, string field, object value)
    {
        var info = instance.GetType().GetField(field, Hidden);
        if (info == null) throw new MissingFieldException(field);
        info.SetValue(instance, value);
    }
    static T Get<T>(object instance, string field)
    {
        var info = instance.GetType().GetField(field, Hidden);
        if (info == null) throw new MissingFieldException(field);
        return (T)info.GetValue(instance);
    }
    static void Call(object instance, string method)
    {
        var info = instance.GetType().GetMethod(method, Hidden);
        if (info == null) throw new MissingMethodException(method);
        try { info.Invoke(instance, null); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
    }
    public static void Run()
    {
        GameObject host = null;
        try
        {
            RulesetDto rules;
            var catalog = CatalogLoader.Load(out rules);
            host = new GameObject("M12 level select probe");
            var bootstrap = host.AddComponent<HabitatBootstrap>();
            Set(bootstrap, "catalog", catalog);
            Set(bootstrap, "rules", rules);
            Set(bootstrap, "save", new SaveV3 { highest = 35, last = 30 });
            Call(bootstrap, "MakeInfra");
            Call(bootstrap, "ShowLevels");
            var canvas = Get<Canvas>(bootstrap, "canvas");
            var grid = canvas.transform.Find("Levels Backdrop/Safe Area/Levels/Level Scroll/Level Grid");
            if (grid == null || grid.childCount != 35) throw new Exception("Level Select must contain 35 cards.");
            foreach (var level in new[] { 31, 32, 33, 34, 35 }) {
                if (level != 31) { Call(bootstrap, "ShowLevels"); grid = canvas.transform.Find("Levels Backdrop/Safe Area/Levels/Level Scroll/Level Grid"); }
                var card = grid.GetChild(level - 1).GetComponent<Button>();
                if (card == null || !card.interactable) throw new Exception("L" + level + " is not unlocked.");
                card.onClick.Invoke();
                var session = Get<ContinuousSession>(bootstrap, "session");
                if (Get<int>(bootstrap, "current") != level || session == null || session.Committed.levelId != level)
                    throw new Exception("L" + level + " card did not open gameplay.");
            }
            Debug.Log("M12_LEVEL_SELECT_PASS cards=35 levels31To35Opened=True");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogError("M12_LEVEL_SELECT_FAIL " + e); EditorApplication.Exit(1); }
        finally { if (host != null) UnityEngine.Object.DestroyImmediate(host); }
    }
}
