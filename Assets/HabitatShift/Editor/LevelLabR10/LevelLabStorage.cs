#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HabitatShift.Core;
using UnityEngine;

namespace HabitatShift.LevelLabR10
{
    public static class R10Storage
    {
        public const string Schema = "r10.0-editor-1.0";
        public static string DefaultRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.docs/LevelLabR10/Projects"));

        public static void ValidateProject(R10Project project)
        {
            if (project == null || project.schema != Schema) throw new InvalidOperationException("Invalid R10 project schema.");
            if (project.level == null) throw new InvalidOperationException("Project has no editable candidate level.");
            ValidateRuntimeReady(project.level);
        }

        public static void Save(R10Project project, string path)
        {
            ValidateProject(project);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(project, true));
        }

        public static R10Project Load(string path)
        {
            var project = JsonUtility.FromJson<R10Project>(File.ReadAllText(path));
            ValidateProject(project);
            return project;
        }

        public static R10Project ImportV5(LevelDto level, string fingerprint)
        {
            if (level == null) throw new InvalidOperationException("The selected V5 catalog level is missing.");
            var editable = Clone(level);
            ValidateRuntimeReady(editable);
            return new R10Project { id = "L" + editable.id + "_DRAFT", level = editable, sourceCatalogFingerprint = fingerprint };
        }

        public static R10Project ImportR9(string json) => R10R9Adapter.Import(json);

        public static R10Project ImportV5Catalog(string catalogPath, int levelId)
        {
            var raw = File.ReadAllText(catalogPath);
            // The production catalog stores coordinate pairs as [x,y]. JsonUtility needs {x,y},
            // exactly as the production CatalogLoader normalizes them before deserialization.
            var normalized = Regex.Replace(raw, @"\[\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\]", "{\"x\":$1,\"y\":$2}");
            var catalog = JsonUtility.FromJson<CatalogDto>(normalized);
            if (catalog == null || catalog.schema != "habitat-shift-levels-v5" || catalog.rulesetVersion != "continuous-core-v3" || catalog.levels == null)
                throw new InvalidOperationException("Import V5 requires a habitat-shift-levels-v5 catalog paired with continuous-core-v3.");
            if (catalog.levelCount != catalog.levels.Length)
                throw new InvalidOperationException("The V5 catalog levelCount does not match its levels array.");
            var level = catalog.levels.FirstOrDefault(x => x != null && x.id == levelId);
            if (level == null) throw new InvalidOperationException("Level not found: " + levelId);
            using (var sha = SHA256.Create())
                return ImportV5(level, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", "").ToLowerInvariant());
        }

        public static LevelDto Clone(LevelDto source) => JsonUtility.FromJson<LevelDto>(JsonUtility.ToJson(source));

        // ContinuousSession assumes non-empty stage and collection arrays. Keep malformed candidates
        // editable only when loaded through a future repair flow; never pass them to runtime preview,
        // risk analysis, or play test where they would break the Editor GUI event.
        public static void ValidateRuntimeReady(LevelDto level)
        {
            if (level == null) throw new InvalidOperationException("Candidate level is missing.");
            if (level.cols < 1 || level.rows < 1) throw new InvalidOperationException("Candidate board dimensions must be positive.");
            if (level.habitats == null) throw new InvalidOperationException("Candidate has no habitats array.");
            if (level.targets == null) throw new InvalidOperationException("Candidate has no targets array.");
            if (level.elevators == null) throw new InvalidOperationException("Candidate has no elevators array.");
            foreach (var habitat in level.habitats)
            {
                if (habitat == null) throw new InvalidOperationException("Candidate contains a null habitat.");
                if (string.IsNullOrEmpty(habitat.id)) throw new InvalidOperationException("Candidate contains a habitat without an ID.");
                if (habitat.shape == null || habitat.shape.Length == 0) throw new InvalidOperationException("Habitat " + habitat.id + " has no footprint.");
                // JsonUtility may materialize an omitted legacy stages field as an empty array.
                // A V5 habitat with valid legacy color/need is still a legitimate one-stage habitat.
                if (habitat.stages != null && habitat.stages.Length == 0)
                {
                    if (string.IsNullOrEmpty(habitat.color) || habitat.need < 1)
                        throw new InvalidOperationException("Habitat " + habitat.id + " declares an empty stages array without valid legacy color/need.");
                    habitat.stages = null;
                }
                if (habitat.stages == null && (string.IsNullOrEmpty(habitat.color) || habitat.need < 1)) throw new InvalidOperationException("Habitat " + habitat.id + " has no valid legacy stage.");
                if (habitat.stages != null && habitat.stages.Any(stage => stage == null || string.IsNullOrEmpty(stage.color) || stage.need < 1)) throw new InvalidOperationException("Habitat " + habitat.id + " has an invalid stage.");
            }
            if (level.elevators.Any(elevator => elevator == null || elevator.queue == null)) throw new InvalidOperationException("Candidate contains an invalid elevator or queue.");
        }

        public static R10Project CloneProject(R10Project project) => JsonUtility.FromJson<R10Project>(JsonUtility.ToJson(project));

        public static HabitatDto CloneHabitat(HabitatDto habitat) => JsonUtility.FromJson<HabitatDto>(JsonUtility.ToJson(habitat));

        public static void ResetCertification(R10Project project)
        {
            project.certification = project.certification ?? new R10Certification();
            project.certification.status = "DRAFT";
            project.certification.manualTest = "NOT_TESTED";
            if (project.certification.trace != null) project.certification.traceStale = true;
        }
    }
}
#endif
