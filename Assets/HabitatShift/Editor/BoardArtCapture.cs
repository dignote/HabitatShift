using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HabitatShift.Runtime;
using UnityEditor;
using UnityEngine;

namespace HabitatShift.Editor
{
    // Write CURRENT or a batch profile to .docs/Verification/capture.request in Play Mode.
    // Screenshots come from Unity's render output, never from the desktop.
    [InitializeOnLoad]
    public static class BoardArtCapture
    {
        static readonly int[] Levels = { 1, 2, 7, 12, 18, 23 };
        static readonly string Folder = Path.Combine(Directory.GetCurrentDirectory(), ".docs", "Verification");
        static readonly string Request = Path.Combine(Folder, "capture.request");
        static readonly MethodInfo StartLevel = typeof(HabitatBootstrap).GetMethod("StartLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly HashSet<string> ReservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static HabitatBootstrap game;
        static string profile;
        static int index, delay, phase;

        static BoardArtCapture() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying) return;
            if (!EditorApplication.isPlaying)
            {
                if (File.Exists(Request))
                {
                    File.Delete(Request);
                    Debug.LogError("Board art capture: enter Play Mode before requesting a screenshot.");
                }
                return;
            }
            if (phase == 0)
            {
                if (!File.Exists(Request)) return;
                // Set-Content creates the file before it finishes writing the command.
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(Request)).TotalMilliseconds < 200) return;
                profile = File.ReadAllText(Request).Trim();
                File.Delete(Request);
                game = UnityEngine.Object.FindAnyObjectByType<HabitatBootstrap>();
                if (string.Equals(profile, "CURRENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (game == null) { Debug.LogError("Board art capture: game is not ready."); return; }
                    if (EditorApplication.isPaused) { Debug.LogError("Board art capture: unpause the Unity Editor before capturing."); return; }
                    var view = FindCaptureView();
                    if (view == null) { Debug.LogError("Board art capture: open a Game or Simulator view before capturing."); return; }
                    view.Focus();
                    game.StartCoroutine(CaptureCurrentFrame());
                    return;
                }
                if (string.IsNullOrEmpty(profile)) profile = "Simulator";
                foreach (var c in Path.GetInvalidFileNameChars()) profile = profile.Replace(c, '_');
                if (game == null || StartLevel == null) { Debug.LogError("Board art capture: game not ready."); return; }
                index = 0;
                phase = 1;
            }
            if (delay-- > 0) return;
            if (phase == 1)
            {
                if (index == Levels.Length) { phase = 0; Debug.Log("Board art capture complete: " + Folder); return; }
                StartLevel.Invoke(game, new object[] { Levels[index] });
                var tutorial = GameObject.Find("Tutorial");
                if (tutorial != null) UnityEngine.Object.Destroy(tutorial);
                delay = 12;
                phase = 2;
            }
            else if (phase == 2)
            {
                Directory.CreateDirectory(Folder);
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder,
                    profile + "_L" + Levels[index].ToString("00") + ".png"));
                index++;
                delay = 12;
                phase = 1;
            }
        }

        static EditorWindow FindCaptureView()
        {
            EditorWindow gameView = null;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var type = window.GetType().Name;
                var title = window.titleContent.text;
                if (type == "SimulatorWindow" || title == "Simulator") return window;
                if (type == "GameView" || title == "Game") gameView = window;
            }
            return gameView;
        }

        static IEnumerator CaptureCurrentFrame()
        {
            yield return null; // Give the Game/Simulator tab one frame to become active.
            yield return new WaitForEndOfFrame();
            if (!EditorApplication.isPlaying) yield break;
            var width = Screen.width;
            var height = Screen.height;
            if (width < 1 || height < 1) { Debug.LogError("Board art capture: Game/Simulator resolution is unavailable."); yield break; }

            Directory.CreateDirectory(Folder);
            var stem = "Current_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + width + "x" + height;
            var path = Path.Combine(Folder, stem + ".png");
            for (var suffix = 2; File.Exists(path) || ReservedPaths.Contains(path); suffix++)
                path = Path.Combine(Folder, stem + "_" + suffix + ".png");
            ReservedPaths.Add(path);
            ScreenCapture.CaptureScreenshot(path);

            for (var frame = 0; frame < 120; frame++)
            {
                yield return null;
                if (!TryReadPngSize(path, out var savedWidth, out var savedHeight)) continue;
                if (savedWidth != width || savedHeight != height)
                    Debug.LogError($"Board art capture: saved {savedWidth}x{savedHeight}, expected {width}x{height}: {path}");
                else
                    Debug.Log($"Board art capture: saved {savedWidth}x{savedHeight}: {path}");
                ReservedPaths.Remove(path);
                yield break;
            }
            ReservedPaths.Remove(path);
            Debug.LogError("Board art capture: screenshot was not written: " + path);
        }

        static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = height = 0;
            try
            {
                if (!File.Exists(path)) return false;
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var header = new byte[24];
                    if (file.Length < 36 || file.Read(header, 0, header.Length) != header.Length ||
                        header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71) return false;
                    file.Seek(-8, SeekOrigin.End);
                    var trailer = new byte[4];
                    if (file.Read(trailer, 0, trailer.Length) != trailer.Length ||
                        trailer[0] != 73 || trailer[1] != 69 || trailer[2] != 78 || trailer[3] != 68) return false;
                    width = ReadBigEndianInt(header, 16);
                    height = ReadBigEndianInt(header, 20);
                    return true;
                }
            }
            catch (IOException) { return false; }
        }

        static int ReadBigEndianInt(byte[] data, int index) =>
            data[index] << 24 | data[index + 1] << 16 | data[index + 2] << 8 | data[index + 3];
    }
}
