using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HabitatShift.Core;
using UnityEditor;
using UnityEngine;

public static class M12RecoveryTraceGenerator
{
    static readonly string[] L32 = { "L32_H_LEFT_ORANGE", "L32_H_RIGHT_BLUE", "L32_H_LEFT_GREEN", "L32_H_RIGHT_PURPLE", "L32_H_LEFT_RED", "L32_H_RIGHT_BROWN", "L32_H_CYAN_RELAY", "L32_H_CYAN_RELAY", "L32_H_PINK_RELAY", "L32_H_PINK_RELAY" };
    static readonly Vector2[] L32To = { new Vector2(.5f,1.5f),new Vector2(5.5f,1.5f),new Vector2(.5f,3.5f),new Vector2(5.5f,3.5f),new Vector2(.5f,5.5f),new Vector2(5.5f,5.5f),new Vector2(.5f,7.5f),new Vector2(1.5f,7.5f),new Vector2(5.5f,7.5f),new Vector2(4.5f,6.5f) };
    static readonly string[] L33 = { "L33_H_STAIR_YELLOW", "L33_H_STAIR_BLUE", "L33_H_OFFSET_PINK", "L33_H_OFFSET_WHITE", "L33_H_LOWER_BROWN", "L33_H_PURPLE_RELAY", "L33_H_PURPLE_RELAY", "L33_H_ORANGE_RELAY", "L33_H_ORANGE_RELAY", "L33_H_GREEN_RELAY", "L33_H_GREEN_RELAY" };
    static readonly Vector2[] L33To = { new Vector2(.5f,1.5f),new Vector2(4.5f,1.5f),new Vector2(.5f,3.5f),new Vector2(4.5f,3.5f),new Vector2(.5f,5.5f),new Vector2(4.5f,5.5f),new Vector2(4.5f,4.5f),new Vector2(.5f,7.5f),new Vector2(1.5f,7.5f),new Vector2(4.5f,7.5f),new Vector2(4.5f,6.5f) };
    public static void Run()
    {
        try
        {
            RulesetDto rules; var catalog=CatalogLoader.Load(out rules);
            WriteSet(catalog, rules, 32, L32, L32To, new[]{0,1,2,3,4,5,6,7,8,9}, new[]{1,0,2,3,4,5,6,7,8,9}, new[]{2,1,0,3,4,5,6,7,8,9});
            WriteSet(catalog, rules, 33, L33, L33To, new[]{0,1,2,3,4,5,6,9,10,7,8}, new[]{1,0,2,3,4,5,6,9,10,7,8}, new[]{2,1,0,3,4,5,6,9,10,7,8});
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogError("M12_RECOVERY_TRACE_FAIL " + e); EditorApplication.Exit(1); }
    }
    static void WriteSet(CatalogDto catalog,RulesetDto rules,int id,string[] ids,Vector2[] to,params int[][] orders)
    {
        var names=new[]{"canonical","altA","altB"};
        for(var i=0;i<orders.Length;i++)
        {
            var trace=Build(catalog.levels[id-1],rules,catalog.canonicalLevelsFingerprint,ids,to,orders[i]);
            var root=Directory.GetParent(Application.dataPath).FullName;
            var path=Path.Combine(root,".docs","LevelTools","fixtures","m12_L"+id+"_solution_trace_v2_"+names[i]+".json");
            File.WriteAllText(path,JsonUtility.ToJson(trace,true)+"\n");
            SolutionTraceRunner.Replay(trace,catalog.levels[id-1],rules,catalog.canonicalLevelsFingerprint);
            Debug.Log("M12_RECOVERY_TRACE_PASS L"+id+" "+names[i]);
        }
    }
    static SolutionTraceV1 Build(LevelDto level,RulesetDto rules,string fingerprint,string[] ids,Vector2[] to,int[] order)
    {
        var s=new ContinuousSession(level,rules);var gs=new List<TraceGestureV1>();
        foreach(var index in order)
        {
            var h=s.Committed.habitats.Find(x=>x.id==ids[index]); if(h==null) throw new Exception("Missing "+ids[index]);
            var press=h.anchor+new Vector2(.5f,.5f); if(!s.BeginDrag(ids[index],press))throw new Exception("Begin "+ids[index]);
            s.MovePointer(to[index]); var resolved=s.ResolvedPose; var sprouts=string.Join(",",s.Preview.sprouts.Select(z=>z.id+":"+z.position)); if(!s.EndDrag())throw new Exception("Commit "+ids[index]+" resolved="+resolved+" sprouts="+sprouts);
            gs.Add(new TraceGestureV1{habitatId=ids[index],press=new FloatPair{x=press.x,y=press.y},boardPath=new[]{new FloatPair{x=to[index].x,y=to[index].y}},checkpoint=SolutionTraceRunner.Fingerprint(s.Committed)});
        }
        if(!s.Committed.won)throw new Exception("L"+level.id+" not won");
        return new SolutionTraceV1{levelId=level.id,catalogFingerprint=fingerprint,gestures=gs.ToArray()};
    }
}
