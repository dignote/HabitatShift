// Build APK Android tu Unity Editor dang mo, kich hoat bang file .docs/Build/apk.request.
// Request JSON: { "output": "<duong dan .apk>", "development": true|false }
// Ket qua ghi vao .docs/Build/apk.status (JSONL) de theo doi tu ben ngoai.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HabitatShift.Editor
{
    [InitializeOnLoad]
    public static class AndroidApkBuild
    {
        static readonly string Root = Path.Combine(Directory.GetCurrentDirectory(), ".docs", "Build");
        static readonly string RequestPath = Path.Combine(Root, "apk.request");
        static readonly string StatusPath = Path.Combine(Root, "apk.status");
        static readonly string ReadyPath = Path.Combine(Root, "apk.ready");
        static bool running, announced;

        static AndroidApkBuild() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!announced)
            {
                announced = true;
                SafeWrite(ReadyPath, DateTime.Now.ToString("o"));
            }
            if (running || !File.Exists(RequestPath)) return;
            DateTime stamp;
            try { stamp = File.GetLastWriteTimeUtc(RequestPath); }
            catch (IOException) { return; }
            if ((DateTime.UtcNow - stamp).TotalMilliseconds < 300) return;
            string json;
            try
            {
                json = File.ReadAllText(RequestPath);
                File.Delete(RequestPath);
            }
            catch (IOException) { return; }
            running = true;
            try { Run(json); }
            catch (Exception error)
            {
                Status("fatal", "", "", error.ToString().Replace("\n", " "));
                Debug.LogError("Android APK build failed: " + error);
            }
            finally { running = false; }
        }

        static void Run(string json)
        {
            var output = Value(json, "output");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("request thieu truong 'output'.");
            var development = !json.Contains("\"development\":false");
            var full = Path.GetFullPath(output);
            var folder = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) scenes.Add(scene.path);
            if (scenes.Count == 0) throw new InvalidOperationException("Build Settings khong co scene nao duoc bat.");

            Status("started", "", full, scenes.Count + " scene | development=" + development);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Status("switching", "", full, "chuyen build target sang Android");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException("Khong chuyen duoc build target sang Android.");
            }
            EditorUserBuildSettings.buildAppBundle = false; // xuat APK, khong phai AAB

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = full,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            var note = summary.result + " size=" + summary.totalSize + " errors=" + summary.totalErrors +
                       " warnings=" + summary.totalWarnings + " time=" + summary.totalTime;
            Status(summary.result == BuildResult.Succeeded ? "succeeded" : "failed", "", full, note);
            if (summary.result == BuildResult.Succeeded) Debug.Log("Android APK build succeeded: " + full);
            else Debug.LogError("Android APK build failed: " + full + " | " + note);
        }

        static void Status(string state, string id, string path, string note)
        {
            var line = "{\"time\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\",\"state\":\"" + Esc(state) +
                       "\",\"path\":\"" + Esc(path) + "\",\"note\":\"" + Esc(note) + "\"}" + Environment.NewLine;
            try
            {
                Directory.CreateDirectory(Root);
                File.AppendAllText(StatusPath, line, Encoding.UTF8);
            }
            catch (IOException) { }
        }

        static string Esc(string value) => (value ?? string.Empty).Replace("\\", "/").Replace("\"", "'").Replace("\n", " ");

        static void SafeWrite(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
            }
            catch (IOException) { }
        }

        static string Value(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
