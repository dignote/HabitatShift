using System;
using System.IO;
using System.Reflection;
using HabitatShift.Runtime;
using UnityEditor;
using UnityEngine;

namespace HabitatShift.Editor
{
    // Development-only proof capture. Put a profile name in .docs/Verification/capture.request
    // while Play Mode is running; screenshots are taken by Unity, not from the desktop.
    [InitializeOnLoad]
    public static class BoardArtCapture
    {
        static readonly int[] Levels = { 1, 2, 7, 12, 18 };
        static readonly string Folder = Path.Combine(Directory.GetCurrentDirectory(), ".docs", "Verification");
        static readonly string Request = Path.Combine(Folder, "capture.request");
        static readonly MethodInfo StartLevel = typeof(HabitatBootstrap).GetMethod("StartLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        static HabitatBootstrap game;
        static string profile;
        static int index, delay, phase;

        static BoardArtCapture() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (phase == 0)
            {
                if (!File.Exists(Request)) return;
                profile = File.ReadAllText(Request).Trim();
                if (string.IsNullOrEmpty(profile)) profile = "Simulator";
                foreach (var c in Path.GetInvalidFileNameChars()) profile = profile.Replace(c, '_');
                File.Delete(Request);
                game = UnityEngine.Object.FindAnyObjectByType<HabitatBootstrap>();
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
    }
}
