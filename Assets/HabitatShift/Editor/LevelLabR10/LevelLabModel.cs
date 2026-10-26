#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HabitatShift.Core;
using UnityEngine;
namespace HabitatShift.LevelLabR10 {
[Serializable] public class R10Project { public string schema="r10.0-editor-1.0",id="DRAFT",archetype="CUSTOM",authoringNotes="",sourceCatalogFingerprint="",source="NEW",sourceCandidateId=""; public LevelDto level; public List<R10Edge> dependencyGraph=new List<R10Edge>(); public R10Certification certification=new R10Certification(); }
[Serializable] public class R10Edge { public string from,to,kind="MOVE_OPENS"; }
[Serializable] public class R10Certification { public string status="DRAFT",manualTest="NOT_TESTED"; public SolutionTraceV1 trace; public bool traceStale; }
[Serializable] public class R10Finding { public string severity,code,detail; }
[Serializable] public class R10Report { public List<R10Finding> findings=new List<R10Finding>(); public int playable,habitats,stages,initialTargets,queued,obstacles,dependencyDepth; public float density; }
public enum R10Risk { PROVEN_DEADLOCK,HIGH_RISK,UNKNOWN,SAFE_BY_TRACE }
public enum R10Tool { Select,Playable,Obstacle,Sproutling,Elevator,Habitat }
public enum R10View { AUTHORING, RUNTIME_PREVIEW, PLAYTEST }
public sealed class R10EditorState : ScriptableObject { public R10Project project; }
public static class R10Rules { public static RulesetDto Default { get { return new RulesetDto { rulesetVersion="continuous-core-v3",dragSweepStep=.08f,collectionTolerance=.12f,foreignSproutlingCollisionRadius=.18f,narrowCorridorDragCollisionInset=.1f,activeDragHabitatCollisionSkin=.04f,strictProjectionRadius=.12f,contactReleaseHysteresis=.2f,strictCorrectionPerSweep=.02f,contactSolverIterations=4 }; } } }
}
#endif