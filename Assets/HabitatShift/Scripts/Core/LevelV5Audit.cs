using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HabitatShift.Core
{
    public sealed class LevelAuditFinding
    {
        public string code, habitatId, targetId, detail;
    }

    // Static authoring checks. Reachability ignores movable neighbors; queue analysis is a
    // color dependency check, not a geometric solver or a certificate of solvability.
    public static class LevelV5Audit
    {
        public static List<LevelAuditFinding> Check(LevelDto level)
        {
            var result = new List<LevelAuditFinding>();
            if (level == null || level.habitats == null || level.targets == null || level.elevators == null)
            {
                Add(result, "SCHEMA", null, null, "Missing level arrays.");
                return result;
            }
            var mask = level.playableMask == null || level.playableMask.Length == 0
                ? null : new HashSet<Vector2Int>(level.playableMask.Select(P));
            var obstacles = new HashSet<Vector2Int>((level.obstacles ?? new IntPair[0]).Select(P));
            if (level.cols < 1 || level.rows < 1 || level.cols > 10 || level.rows > 10 ||
                mask != null && mask.Count != level.playableMask.Length ||
                obstacles.Count != (level.obstacles ?? new IntPair[0]).Length)
                Add(result, "MASK", null, null, "Invalid dimensions or duplicate mask/obstacle cells.");
            foreach (var cell in (mask ?? new HashSet<Vector2Int>()).Concat(obstacles))
                if (cell.x < 0 || cell.y < 0 || cell.x >= level.cols || cell.y >= level.rows ||
                    obstacles.Contains(cell) && mask != null && !mask.Contains(cell))
                    Add(result, "MASK", null, null, "Mask or obstacle cell is outside the playable board.");
            var need = new Dictionary<string, int>();
            var supply = new Dictionary<string, int>();
            var occupied = new HashSet<Vector2Int>();
            foreach (var target in level.targets)
                if (target == null || target.position == null || string.IsNullOrEmpty(target.id) || string.IsNullOrEmpty(target.color))
                    Add(result, "SCHEMA", null, target == null ? null : target.id, "Invalid target.");
            foreach (var elevator in level.elevators)
                if (elevator == null || elevator.entry == null || elevator.queue == null || elevator.nextIndex < 0 ||
                    elevator.queue != null && elevator.nextIndex > elevator.queue.Length)
                    Add(result, "SCHEMA", null, elevator == null ? null : elevator.id, "Invalid elevator.");
            foreach (var habitat in level.habitats)
            {
                if (habitat == null || habitat.shape == null || habitat.shape.Length == 0 || habitat.anchor == null)
                {
                    Add(result, "SCHEMA", habitat == null ? null : habitat.id, null, "Invalid habitat geometry.");
                    continue;
                }
                var stages = habitat.stages ?? new[] { new HabitatStageDto { color = habitat.color, need = habitat.need } };
                if (stages.Length < 1 || stages.Length > 2 || stages.Any(x => x == null || x.need < 1 || string.IsNullOrEmpty(x.color)) ||
                    stages.Length == 2 && stages[0].color == stages[1].color)
                    Add(result, "STAGES", habitat.id, null, "Invalid stage sequence.");
                foreach (var stage in stages.Where(x => x != null && x.need > 0 && !string.IsNullOrEmpty(x.color)))
                    need[stage.color] = Get(need, stage.color) + stage.need;
                var footprint = habitat.shape.Select(P).ToArray();
                var origin = P(habitat.anchor);
                if (!Legal(level, footprint, origin, mask, obstacles)) Add(result, "PLACEMENT", habitat.id, null, "Initial anchor is outside the legal mask or on an obstacle.");
                foreach (var cell in footprint)
                    if (!occupied.Add(origin + cell)) Add(result, "PLACEMENT", habitat.id, null, "Initial habitat footprints overlap.");
                var reachable = Reachable(level, habitat, mask, obstacles);
                if (reachable.Count <= 1) Add(result, "MOBILITY", habitat.id, null, "Habitat has no adjacent legal anchor.");
                foreach (var target in level.targets.Where(x => x != null && stages.Any(s => s != null && s.color == x.color)))
                    if (!reachable.Any(a => Covers(footprint, a, target.position)))
                        Add(result, "REACHABILITY", habitat.id, target.id, "Matching target is unreachable by this habitat in the static mask.");
                foreach (var elevator in level.elevators.Where(x => x != null && x.queue != null))
                    foreach (var item in elevator.queue.Skip(elevator.nextIndex).Where(x => x != null && stages.Any(s => s != null && s.color == x.color)))
                        if (!reachable.Any(a => Covers(footprint, a, elevator.entry)))
                            Add(result, "REACHABILITY", habitat.id, item.id, "Matching queued target entry is unreachable by this habitat in the static mask.");
            }
            foreach (var target in level.targets.Where(x => x != null && !string.IsNullOrEmpty(x.color))) supply[target.color] = Get(supply, target.color) + 1;
            var occupiedTargetCells = new Dictionary<Vector2Int, string>();
            foreach (var target in level.targets.Where(x => x != null && x.position != null))
            {
                var targetCell = new Vector2Int(Mathf.FloorToInt(target.position.x), Mathf.FloorToInt(target.position.y));
                string existingTarget;
                if (occupiedTargetCells.TryGetValue(targetCell, out existingTarget))
                    Add(result, "DUPLICATE_TARGET_POSITION", null, target.id,
                        "Initial target shares board cell with " + existingTarget + ".");
                else occupiedTargetCells[targetCell] = target.id;
                if (targetCell.x < 0 || targetCell.y < 0 || targetCell.x >= level.cols || targetCell.y >= level.rows ||
                    mask != null && !mask.Contains(targetCell) || obstacles.Contains(targetCell))
                    Add(result, "PLACEMENT", null, target.id, "Initial target is outside playable space.");
                if (occupied.Contains(new Vector2Int(Mathf.FloorToInt(target.position.x), Mathf.FloorToInt(target.position.y))))
                    Add(result, "PLACEMENT", null, target.id, "Initial target overlaps a habitat.");
            }
            foreach (var elevator in level.elevators.Where(x => x != null && x.queue != null))
            {
                if (elevator.entry == null) Add(result, "SCHEMA", null, elevator.id, "Missing elevator entry.");
                else
                {
                    var entryCell = new Vector2Int(Mathf.FloorToInt(elevator.entry.x), Mathf.FloorToInt(elevator.entry.y));
                    if (entryCell.x < 0 || entryCell.y < 0 || entryCell.x >= level.cols || entryCell.y >= level.rows ||
                        mask != null && !mask.Contains(entryCell) || obstacles.Contains(entryCell))
                        Add(result, "PLACEMENT", null, elevator.id, "Elevator entry is outside playable space.");
                    if (occupied.Contains(entryCell)) Add(result, "PLACEMENT", null, elevator.id, "Elevator entry overlaps a habitat.");
                }
                foreach (var item in elevator.queue.Skip(elevator.nextIndex).Where(x => x != null && !string.IsNullOrEmpty(x.color)))
                    supply[item.color] = Get(supply, item.color) + 1;
            }
            foreach (var item in need) if (Get(supply, item.Key) != item.Value)
                Add(result, "SUPPLY", null, null, item.Key + " needs " + item.Value + ", supply " + Get(supply, item.Key) + ".");
            foreach (var item in supply) if (!need.ContainsKey(item.Key))
                Add(result, "SUPPLY", null, null, item.Key + " has supply without a Habitat stage.");
            CheckQueueDependencies(level, result);
            return result;
        }

        public static Dictionary<string, HashSet<Vector2Int>> ReachabilityByHabitat(LevelDto level)
        {
            var mask = level.playableMask == null || level.playableMask.Length == 0 ? null : new HashSet<Vector2Int>(level.playableMask.Select(P));
            var obstacles = new HashSet<Vector2Int>((level.obstacles ?? new IntPair[0]).Select(P));
            return level.habitats.ToDictionary(x => x.id, x => Reachable(level, x, mask, obstacles));
        }

        // Resource-order search only: geometry is intentionally outside this bounded audit.
        // A terminal path proves that colors/stages/queues can be ordered; it is not a solve certificate.
        static void CheckQueueDependencies(LevelDto level, List<LevelAuditFinding> result)
        {
            const int nodeLimit = 1000000;
            var stages = level.habitats.Select(h => h == null ? new HabitatStageDto[0] :
                (h.stages ?? new[] { new HabitatStageDto { color = h.color, need = h.need } })).ToArray();
            var initial = new Dictionary<string, int>();
            foreach (var target in level.targets.Where(x => x != null && !string.IsNullOrEmpty(x.color)))
                initial[target.color] = Get(initial, target.color) + 1;
            var queue = new Queue<QueueState>();
            queue.Enqueue(new QueueState(level, stages, initial));
            var seen = new HashSet<string>();
            var nodes = 0;
            while (queue.Count > 0)
            {
                if (++nodes > nodeLimit)
                {
                    Add(result, "SEARCH_LIMIT_REACHED", null, null,
                        "Queue/stage resource search reached " + nodeLimit + " states; not certified.");
                    return;
                }
                var state = queue.Dequeue();
                var key = state.Key();
                if (!seen.Add(key)) continue;
                if (state.Complete(stages)) return; // DEPENDENCY_PATH_FOUND (no fatal finding)
                // An elevator can expose only one queued sprout at a time.
                for (var e = 0; e < level.elevators.Length; e++)
                    if (state.pending[e] == null && state.positions[e] < (level.elevators[e].queue ?? new QueueItemDto[0]).Length)
                    {
                        var next = state.Clone();
                        next.pending[e] = level.elevators[e].queue[next.positions[e]++].color;
                        queue.Enqueue(next);
                    }
                for (var h = 0; h < stages.Length; h++)
                {
                    if (state.stage[h] >= stages[h].Length) continue;
                    var active = stages[h][state.stage[h]];
                    if (active == null || state.remaining[h] < 1) continue;
                    int available;
                    if (state.available.TryGetValue(active.color, out available) && available > 0)
                    {
                        var next = state.Clone(); var amount = Math.Min(available, next.remaining[h]);
                        next.available[active.color] -= amount; next.Consume(h, amount, stages); queue.Enqueue(next);
                    }
                    for (var e = 0; e < state.pending.Length; e++)
                        if (state.pending[e] == active.color)
                        {
                            var next = state.Clone(); next.pending[e] = null; next.Consume(h, 1, stages); queue.Enqueue(next);
                        }
                }
            }
            Add(result, "PROVEN_QUEUE_STAGE_BLOCKER", null, null,
                "No finite queue/stage resource ordering reaches every stage.");
        }

        sealed class QueueState
        {
            public Dictionary<string, int> available;
            public int[] positions, stage, remaining;
            public string[] pending;
            public QueueState(LevelDto level, HabitatStageDto[][] stages, Dictionary<string, int> initial)
            {
                available = new Dictionary<string, int>(initial);
                positions = level.elevators.Select(e => e == null ? 0 : e.nextIndex).ToArray();
                pending = new string[level.elevators.Length];
                stage = new int[stages.Length];
                remaining = stages.Select(x => x.Length == 0 || x[0] == null ? 0 : x[0].need).ToArray();
            }
            QueueState() { }
            public QueueState Clone() { return new QueueState {
                available = new Dictionary<string, int>(available), positions = (int[])positions.Clone(),
                pending = (string[])pending.Clone(), stage = (int[])stage.Clone(), remaining = (int[])remaining.Clone() }; }
            public void Consume(int habitat, int amount, HabitatStageDto[][] stages)
            {
                if ((remaining[habitat] -= amount) != 0) return;
                stage[habitat]++;
                if (stage[habitat] < stages[habitat].Length) remaining[habitat] = stages[habitat][stage[habitat]].need;
            }
            public bool Complete(HabitatStageDto[][] stages)
            {
                for (var i = 0; i < stages.Length; i++) if (stage[i] < stages[i].Length) return false;
                return pending.All(x => x == null);
            }
            public string Key()
            {
                return string.Join(",", stage) + "/" + string.Join(",", remaining) + "/" +
                    string.Join(",", positions) + "/" + string.Join(",", pending.Select(x => x ?? "-")) + "/" +
                    string.Join(",", available.OrderBy(x => x.Key).Select(x => x.Key + ":" + x.Value));
            }
        }

        static HashSet<Vector2Int> Reachable(LevelDto level, HabitatDto habitat, HashSet<Vector2Int> mask, HashSet<Vector2Int> obstacles)
        {
            var footprint = habitat.shape.Select(P).ToArray();
            var start = P(habitat.anchor);
            var seen = new HashSet<Vector2Int>();
            if (!Legal(level, footprint, start, mask, obstacles)) return seen;
            var q = new Queue<Vector2Int>(); q.Enqueue(start); seen.Add(start);
            while (q.Count > 0)
            {
                var anchor = q.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    if (habitat.movementConstraint == "HORIZONTAL_ONLY" && d.y != 0 || habitat.movementConstraint == "VERTICAL_ONLY" && d.x != 0) continue;
                    var next = anchor + d;
                    if (Legal(level, footprint, next, mask, obstacles) && seen.Add(next)) q.Enqueue(next);
                }
            }
            return seen;
        }

        static bool Legal(LevelDto level, Vector2Int[] footprint, Vector2Int anchor, HashSet<Vector2Int> mask, HashSet<Vector2Int> obstacles)
        {
            return footprint.All(c => { var p = c + anchor; return p.x >= 0 && p.y >= 0 && p.x < level.cols && p.y < level.rows &&
                (mask == null || mask.Contains(p)) && !obstacles.Contains(p); });
        }

        static bool Covers(Vector2Int[] footprint, Vector2Int anchor, FloatPair target)
        {
            if (target == null) return false;
            return footprint.Any(c => target.x > anchor.x + c.x && target.x < anchor.x + c.x + 1 &&
                target.y > anchor.y + c.y && target.y < anchor.y + c.y + 1);
        }

        static Vector2Int P(IntPair pair) { return new Vector2Int(pair.x, pair.y); }
        static int Get(Dictionary<string, int> counts, string color) { return color != null && counts.TryGetValue(color, out var n) ? n : 0; }
        static void Add(List<LevelAuditFinding> result, string code, string habitatId, string targetId, string detail)
        { result.Add(new LevelAuditFinding { code = code, habitatId = habitatId, targetId = targetId, detail = detail }); }
    }
}
