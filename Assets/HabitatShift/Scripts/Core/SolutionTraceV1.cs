using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace HabitatShift.Core
{
    [Serializable] public sealed class TraceGestureV1
    {
        public string habitatId;
        public FloatPair press;
        public FloatPair[] boardPath;
        public string checkpoint;
    }

    [Serializable] public sealed class SolutionTraceV1
    {
        public int version = 1, levelId;
        public string catalogFingerprint;
        public TraceGestureV1[] gestures;
    }

    public static class SolutionTraceRunner
    {
        public static RuntimeState Replay(SolutionTraceV1 trace, LevelDto level, RulesetDto rules, string catalogFingerprint)
        {
            if (trace == null || trace.version != 1 || trace.levelId != level.id ||
                string.IsNullOrEmpty(catalogFingerprint) || trace.catalogFingerprint != catalogFingerprint || trace.gestures == null)
                throw new InvalidOperationException("Invalid solution trace identity.");
            var session = new ContinuousSession(level, rules);
            for (var i = 0; i < trace.gestures.Length; i++)
            {
                var gesture = trace.gestures[i];
                if (gesture == null || gesture.press == null || gesture.boardPath == null || gesture.boardPath.Length == 0 ||
                    string.IsNullOrEmpty(gesture.checkpoint) ||
                    !session.BeginDrag(gesture.habitatId, Point(gesture.press)))
                    throw new InvalidOperationException("Invalid trace gesture " + i + ".");
                foreach (var point in gesture.boardPath) session.MovePointer(Point(point));
                if (!session.EndDrag()) throw new InvalidOperationException("Trace gesture did not commit: " + i + ".");
                if (Fingerprint(session.Committed) != gesture.checkpoint)
                    throw new InvalidOperationException("Trace checkpoint differs after gesture " + i + ".");
            }
            if (!session.Committed.won) throw new InvalidOperationException("Solution trace did not win.");
            return session.Committed.Clone();
        }

        public static string Fingerprint(RuntimeState state)
        {
            if (state == null) throw new ArgumentNullException("state");
            var b = new StringBuilder();
            b.Append(state.levelId).Append('|').Append(state.moves).Append('|').Append(state.won);
            foreach (var h in state.habitats.OrderBy(x => x.id, StringComparer.Ordinal))
            {
                b.Append("|H:").Append(h.id).Append(':').Append(h.anchor.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                 .Append(',').Append(h.anchor.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                 .Append(':').Append(h.stageIndex);
                foreach (var stage in h.stages) b.Append(':').Append(stage.color).Append(',').Append(stage.remaining);
                foreach (var cell in h.shape.OrderBy(x => x.y).ThenBy(x => x.x)) b.Append(':').Append(cell.x).Append(',').Append(cell.y);
            }
            foreach (var s in state.sprouts.OrderBy(x => x.id, StringComparer.Ordinal))
                b.Append("|S:").Append(s.id).Append(':').Append(s.color).Append(':')
                 .Append(s.position.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                 .Append(s.position.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            foreach (var e in state.elevators.OrderBy(x => x.id, StringComparer.Ordinal)) b.Append("|E:").Append(e.id).Append(':').Append(e.nextIndex);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        static Vector2 Point(FloatPair p)
        {
            if (p == null || float.IsNaN(p.x) || float.IsNaN(p.y)) throw new InvalidOperationException("Invalid trace board coordinate.");
            return new Vector2(p.x, p.y);
        }
    }
}
