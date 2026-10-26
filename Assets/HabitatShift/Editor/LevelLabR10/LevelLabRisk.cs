#if UNITY_EDITOR
using System;
using System.Linq;
using HabitatShift.Core;
using UnityEngine;

namespace HabitatShift.LevelLabR10
{
    public static class R10Solver
    {
        public static bool Replay(SolutionTraceV1 trace, LevelDto level, RulesetDto rules, string fingerprint, out string error)
        {
            try { SolutionTraceRunner.Replay(trace, level, rules, fingerprint); error = null; return true; }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public static R10Risk Risk(R10Project project, RulesetDto rules)
        {
            try
            {
                R10Storage.ValidateRuntimeReady(project.level);
                if (project.certification != null && !project.certification.traceStale && project.certification.trace != null && Replay(project.certification.trace, project.level, rules, project.sourceCatalogFingerprint, out _)) return R10Risk.SAFE_BY_TRACE;
                foreach (var habitat in project.level.habitats ?? Array.Empty<HabitatDto>()) if (CanMove(project.level, rules, habitat.id)) return R10Risk.UNKNOWN;
                return R10Risk.HIGH_RISK;
            }
            catch
            {
                // Risk is advisory. A malformed draft must remain visible and repairable in the Lab.
                return R10Risk.UNKNOWN;
            }
        }

        static bool CanMove(LevelDto level, RulesetDto rules, string id)
        {
            foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var session = new ContinuousSession(level, rules);
                var habitat = session.Committed.habitats.FirstOrDefault(item => item.id == id);
                if (habitat == null) continue;
                var pointer = habitat.anchor + Vector2.one * .5f;
                if (session.BeginDrag(id, pointer))
                {
                    session.MovePointer(pointer + direction);
                    if (session.EndDrag(false)) return true;
                }
            }
            return false;
        }
    }
}
#endif
