#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HabitatShift.Core;
using UnityEngine;
namespace HabitatShift.LevelLabR10 {
[Serializable] sealed class R9ProjectDto { public R9LevelDto[] candidateLevels; public R9LevelDto[] sourceLevels; }
[Serializable] sealed class R9LevelDto { public int id,cols,rows; public R9HabitatDto[] habitats; public R9TargetDto[] targets; public IntPair[] mask,blocked; public R9PortDto[] ports; }
[Serializable] sealed class R9HabitatDto { public string id,color,constraint; public int need; public IntPair anchor; public IntPair[] shape; }
[Serializable] sealed class R9TargetDto { public string id,color; public FloatPair position; }
[Serializable] sealed class R9PortDto { public string id,edge; public IntPair entryCell; public string[] queue; public bool mandatory=true,unknown; }
public static class R10R9Adapter {
 static readonly Regex Pair = new Regex(@"\[\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\]",RegexOptions.Compiled);
 // R9 pair arrays are normalized only in memory; input bytes are never altered on disk.
 static string NormalizePairs(string json) { return Pair.Replace(json,m=>"{\"x\":"+m.Groups[1].Value+",\"y\":"+m.Groups[2].Value+"}"); }
 public static R10Project Import(string json) {
  var source=JsonUtility.FromJson<R9ProjectDto>(NormalizePairs(json));
  if(source==null||source.candidateLevels==null||source.candidateLevels.Length==0) throw new InvalidOperationException("R9 candidateLevels is required; sourceLevels is reference-only.");
  var r=source.candidateLevels[0]; var diagnostics=new List<string>(); var elevators=new List<ElevatorDto>();
  foreach(var port in r.ports??new R9PortDto[0]) { if(port==null||port.unknown||port.entryCell==null||port.queue==null||port.queue.Length==0) { diagnostics.Add("Ignored R9 placeholder/unknown port "+(port==null?"<null>":port.id)); continue; } elevators.Add(new ElevatorDto { id=string.IsNullOrEmpty(port.id)?"R9_PORT_"+elevators.Count:port.id,direction=Direction(port.edge),entry=new FloatPair{x=port.entryCell.x+.5f,y=port.entryCell.y+.5f},mandatory=port.mandatory,nextIndex=0,queue=port.queue.Select((color,index)=>new QueueItemDto{id=(string.IsNullOrEmpty(port.id)?"R9_PORT":port.id)+"_Q"+(index+1).ToString("00"),color=color}).ToArray()}); }
  var level=new LevelDto { id=r.id,cols=r.cols,rows=r.rows,playableMask=r.mask??new IntPair[0],obstacles=r.blocked??new IntPair[0],habitats=(r.habitats??new R9HabitatDto[0]).Select(h=>new HabitatDto{id=h.id,color=h.color,need=h.need,movementConstraint=string.IsNullOrEmpty(h.constraint)?"FREE":h.constraint,anchor=h.anchor,shape=h.shape??new IntPair[0]}).ToArray(),targets=(r.targets??new R9TargetDto[0]).Select(t=>new TargetDto{id=t.id,color=t.color,position=t.position}).ToArray(),elevators=elevators.ToArray() };
  return new R10Project{id="R9_L"+r.id+"_DRAFT",level=level,authoringNotes=string.Join("\n",diagnostics)};
 }
 static string Direction(string edge) { switch((edge??"").ToLowerInvariant()) { case "top":return "DOWN";case "bottom":return "UP";case "left":return "RIGHT";case "right":return "LEFT";default:return "DOWN";} }
}
}
#endif