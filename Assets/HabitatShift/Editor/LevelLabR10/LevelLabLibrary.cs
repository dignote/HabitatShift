#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HabitatShift.Core;
using UnityEditor;
using UnityEngine;
namespace HabitatShift.LevelLabR10
{
    [Serializable] public sealed class R10LevelLibrary { public List<R10LibraryEntry> entries = new List<R10LibraryEntry>(); }
    [Serializable] public sealed class R10LibraryEntry { public bool selected; public string key; public R10Project project; }
    public static class R10LibraryStore
    {
        static R10LevelLibrary cache;
        static string FilePath => Path.Combine(R10Storage.DefaultRoot, "..", "Library", "level_library.json");
        public static R10LevelLibrary Library
        {
            get
            {
                if (cache != null) return cache;
                try { cache = File.Exists(FilePath) ? JsonUtility.FromJson<R10LevelLibrary>(File.ReadAllText(FilePath)) : null; }
                catch (Exception exception) { Debug.LogWarning("Level Lab library could not be read: " + exception.Message); }
                return cache ?? (cache = new R10LevelLibrary());
            }
        }
        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(Library, true));
        }
        public static int AddImported(IEnumerable<R10Project> projects)
        {
            var added = 0;
            foreach (var source in projects)
            {
                var key = "V5/L" + source.level.id;
                var existing = Library.entries.FirstOrDefault(entry => entry.key == key);
                if (existing != null) continue; // Do not overwrite an already persisted user library item.
                var project = R10Storage.CloneProject(source);
                project.source = "IMPORTED_V5";
                Library.entries.Add(new R10LibraryEntry { key = key, project = project });
                added++;
            }
            Save();
            return added;
        }
        public static void DeleteSelected()
        {
            Library.entries.RemoveAll(entry => entry.selected);
            Save();
        }
        public static void SetAll(bool selected)
        {
            foreach (var entry in Library.entries) entry.selected = selected;
            Save();
        }
    }

    public sealed class R10LevelLibraryWindow : EditorWindow
    {
        Vector2 scroll;
        Action<R10Project> open;
        public static void Open(Action<R10Project> onOpen)
        {
            var window = GetWindow<R10LevelLibraryWindow>("Level Library");
            window.open = onOpen;
            window.minSize = new Vector2(700, 360);
            window.Show();
        }
        void OnGUI()
        {
            var library = R10LibraryStore.Library;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("LEVEL LIBRARY ? " + library.entries.Count + " persisted drafts", EditorStyles.toolbarButton);
            if (GUILayout.Button("SELECT ALL", EditorStyles.toolbarButton)) R10LibraryStore.SetAll(true);
            if (GUILayout.Button("CLEAR SELECTION", EditorStyles.toolbarButton)) R10LibraryStore.SetAll(false);
            GUI.enabled = library.entries.Any(entry => entry.selected);
            if (GUILayout.Button("DELETE SELECTED", EditorStyles.toolbarButton))
            {
                if (EditorUtility.DisplayDialog("Delete library drafts", "Delete the selected Level Lab drafts? Production catalog files will not change.", "Delete", "Cancel")) R10LibraryStore.DeleteSelected();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("", GUILayout.Width(26)); GUILayout.Label("Level", GUILayout.Width(60)); GUILayout.Label("Title", GUILayout.Width(220)); GUILayout.Label("Source", GUILayout.Width(110)); GUILayout.Label("Board", GUILayout.Width(65)); GUILayout.Label("Certification", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var entry in library.entries.OrderBy(item => item.project.level.id).ToArray())
            {
                var project = entry.project;
                var level = project.level;
                EditorGUILayout.BeginHorizontal("box");
                var next = GUILayout.Toggle(entry.selected, GUIContent.none, GUILayout.Width(26));
                if (next != entry.selected) { entry.selected = next; R10LibraryStore.Save(); }
                GUILayout.Label(level.id.ToString(), GUILayout.Width(60));
                GUILayout.Label(level.title ?? project.id, GUILayout.Width(220));
                GUILayout.Label(project.source, GUILayout.Width(110));
                GUILayout.Label(level.cols + "x" + level.rows, GUILayout.Width(65));
                GUILayout.Label(project.certification.status, GUILayout.Width(120));
                if (GUILayout.Button("OPEN", GUILayout.Width(70))) open?.Invoke(R10Storage.CloneProject(project));
                if (GUILayout.Button("DELETE", GUILayout.Width(70))) { entry.selected = true; R10LibraryStore.DeleteSelected(); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("Library entries are Level Lab drafts stored under .docs/LevelLabR10/Library. They never modify the production catalog.", MessageType.Info);
        }
    }
}
#endif