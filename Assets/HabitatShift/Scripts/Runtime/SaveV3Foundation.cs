using System;
using System.Collections.Generic;
using System.Linq;
using HabitatShift.Core;

namespace HabitatShift.Runtime
{
    // Production save format for the v5/v3 catalog. SaveV2 remains readable for migration.
    [Serializable]
    public sealed class SaveV3
    {
        public int version = 3, highest = 1, last = 1, uiSize = 1;
        public bool music = true, sfx = true, haptics = true, vfx = true, reducedMotion;
        public List<int> completed = new List<int>();
        public List<Best> best = new List<Best>();
        public List<Charge> charges = new List<Charge>();
        public RuntimeState snapshot;
        public int snapshotLevel;
        public string snapshotSchema, snapshotRuleset, snapshotCatalogFingerprint;
    }

    public static class SaveV3Migration
    {
        public static SaveV3 FromV2(SaveV2 source)
        {
            if (source == null || source.version != 2) throw new InvalidOperationException("Expected SaveV2.");
            var result = new SaveV3
            {
                highest = source.highest, last = source.last, uiSize = source.uiSize,
                music = source.music, sfx = source.sfx, haptics = source.haptics,
                vfx = source.vfx, reducedMotion = source.reducedMotion,
                completed = source.completed == null ? new List<int>() : new List<int>(source.completed),
                best = source.best == null ? new List<Best>() : source.best.Select(x => new Best { level = x.level, moves = x.moves }).ToList(),
                charges = source.charges == null ? new List<Charge>() : source.charges.Select(x => new Charge { level = x.level, nest = x.nest, garden = x.garden, trim = x.trim }).ToList(),
                snapshot = source.snapshot == null ? null : source.snapshot.Clone(), snapshotLevel = source.snapshotLevel,
                snapshotSchema = "habitat-shift-levels-v4", snapshotRuleset = "continuous-core-v2"
            };
            // SaveV2 capped highest at 30, including after completing L30.
            if (result.completed.Contains(30)) result.highest = Math.Max(result.highest, 31);
            if (result.snapshot != null) foreach (var habitat in result.snapshot.habitats) habitat.NormalizeLegacy();
            Validate(result);
            return result;
        }

        public static SaveV3 WithSnapshot(SaveV3 save, RuntimeState state, int level, string schema, string ruleset, string catalogFingerprint = null)
        {
            if (save == null || state == null) throw new ArgumentNullException(save == null ? "save" : "state");
            var copy = UnityEngine.JsonUtility.FromJson<SaveV3>(UnityEngine.JsonUtility.ToJson(save));
            copy.snapshot = state.Clone();
            copy.snapshotLevel = level;
            copy.snapshotSchema = schema;
            copy.snapshotRuleset = ruleset;
            copy.snapshotCatalogFingerprint = catalogFingerprint;
            Validate(copy);
            return copy;
        }

        public static SaveV3 ForCatalog(SaveV3 save, string schema, string ruleset,
            string catalogFingerprint, string previousFingerprint, int changedLevel,
            params string[] earlierFingerprints)
        {
            Validate(save);
            if (save.snapshot == null) return save;
            var copy = UnityEngine.JsonUtility.FromJson<SaveV3>(UnityEngine.JsonUtility.ToJson(save));
            var same = copy.snapshotCatalogFingerprint == catalogFingerprint;
            var preserved = copy.snapshotLevel != changedLevel && copy.snapshotLevel >= 1 &&
                (string.IsNullOrEmpty(copy.snapshotCatalogFingerprint) ||
                 copy.snapshotCatalogFingerprint == previousFingerprint ||
                 earlierFingerprints != null && earlierFingerprints.Contains(copy.snapshotCatalogFingerprint));
            if (copy.snapshotSchema != schema || copy.snapshotRuleset != ruleset || !same && !preserved)
            {
                copy.snapshot = null;
                copy.snapshotLevel = 0;
                copy.snapshotSchema = null;
                copy.snapshotRuleset = null;
                copy.snapshotCatalogFingerprint = null;
            }
            else copy.snapshotCatalogFingerprint = catalogFingerprint;
            Validate(copy);
            return copy;
        }

        public static SaveV3 ForCatalogChangedLevels(SaveV3 save, string schema, string ruleset,
            string catalogFingerprint, int levelCount, int[] changedLevels, params string[] previousFingerprints)
        {
            Validate(save);
            if (save.snapshot == null) return save;
            var copy = UnityEngine.JsonUtility.FromJson<SaveV3>(UnityEngine.JsonUtility.ToJson(save));
            var same = copy.snapshotCatalogFingerprint == catalogFingerprint;
            var fromKnownCatalog = string.IsNullOrEmpty(copy.snapshotCatalogFingerprint) ||
                previousFingerprints != null && previousFingerprints.Contains(copy.snapshotCatalogFingerprint);
            var preserved = fromKnownCatalog && copy.snapshotLevel >= 1 && copy.snapshotLevel <= levelCount &&
                (changedLevels == null || !changedLevels.Contains(copy.snapshotLevel));
            if (copy.snapshotSchema != schema || copy.snapshotRuleset != ruleset || !same && !preserved)
            {
                copy.snapshot = null;
                copy.snapshotLevel = 0;
                copy.snapshotSchema = null;
                copy.snapshotRuleset = null;
                copy.snapshotCatalogFingerprint = null;
            }
            else copy.snapshotCatalogFingerprint = catalogFingerprint;
            Validate(copy);
            return copy;
        }

        public static RuntimeState Restore(SaveV3 save, string schema, string ruleset)
        {
            Validate(save);
            if (save.snapshot == null) return null;
            if (save.snapshotSchema != schema || save.snapshotRuleset != ruleset)
                throw new InvalidOperationException("Save snapshot version does not match catalog and ruleset.");
            return save.snapshot.Clone();
        }

        public static void Validate(SaveV3 save)
        {
            if (save == null || save.version != 3) throw new InvalidOperationException("Invalid SaveV3 version.");
            if (save.snapshot == null) return;
            var legacy = save.snapshotSchema == "habitat-shift-levels-v4" && save.snapshotRuleset == "continuous-core-v2";
            var staged = save.snapshotSchema == "habitat-shift-levels-v5" && save.snapshotRuleset == "continuous-core-v3";
            if (!legacy && !staged || save.snapshot.levelId != save.snapshotLevel)
                throw new InvalidOperationException("Invalid SaveV3 snapshot identity.");
            foreach (var habitat in save.snapshot.habitats)
            {
                if (habitat.stages == null || habitat.stages.Count < 1 || habitat.stages.Count > (staged ? 2 : 1) ||
                    habitat.stageIndex < 0 || habitat.stageIndex >= habitat.stages.Count ||
                    habitat.stages.Any(x => x == null || string.IsNullOrEmpty(x.color) || x.remaining < 0) ||
                    habitat.stages.Count == 2 && habitat.stages[0].color == habitat.stages[1].color ||
                    habitat.stages.Take(habitat.stageIndex).Any(x => x.remaining != 0) ||
                    habitat.ActiveRemaining < 1 || habitat.color != habitat.ActiveColor || habitat.remaining != habitat.ActiveRemaining)
                    throw new InvalidOperationException("Invalid SaveV3 habitat stage.");
            }
        }
    }
}
