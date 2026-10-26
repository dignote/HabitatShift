#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text.RegularExpressions;using HabitatShift.Core;using UnityEditor;using UnityEngine;
namespace HabitatShift.LevelLabR10
{
    public sealed class R10ImportV5Picker : EditorWindow
    {
        CatalogDto catalog; string path; Vector2 scroll; readonly HashSet<int> selected = new HashSet<int>(); Action<List<R10Project>> complete;
        public static void Open(string catalogPath, Action<List<R10Project>> callback)
        {
            var raw = File.ReadAllText(catalogPath);
            var normalized = Regex.Replace(raw, @"\[\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*\]", "{\"x\":$1,\"y\":$2}");
            var catalog = JsonUtility.FromJson<CatalogDto>(normalized);
            if (catalog == null || catalog.schema != "habitat-shift-levels-v5" || catalog.rulesetVersion != "continuous-core-v3" || catalog.levels == null) { EditorUtility.DisplayDialog("Import V5", "Select a valid V5 / continuous-core-v3 catalog.", "OK"); return; }
            var window = CreateInstance<R10ImportV5Picker>(); window.catalog = catalog; window.path = catalogPath; window.complete = callback; window.titleContent = new GUIContent("Import V5 levels"); window.minSize = new Vector2(760, 420); window.ShowUtility();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Select V5 levels to add to the persistent Level Library", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("SELECT ALL", EditorStyles.toolbarButton)) { selected.Clear(); foreach (var level in catalog.levels) selected.Add(level.id); }
            if (GUILayout.Button("CLEAR", EditorStyles.toolbarButton)) selected.Clear();
            GUILayout.FlexibleSpace(); GUILayout.Label(selected.Count + " selected", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar); foreach (var header in new[]{"", "ID", "Title", "Difficulty", "Profile", "Board", "Habitats", "Stages", "Sprouts", "Elevators"}) GUILayout.Label(header, EditorStyles.miniLabel, GUILayout.Width(header=="Title"?190:header==""?28:65)); EditorGUILayout.EndHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var level in catalog.levels)
            {
                EditorGUILayout.BeginHorizontal("box"); var isSelected = GUILayout.Toggle(selected.Contains(level.id), GUIContent.none, GUILayout.Width(28)); if (isSelected) selected.Add(level.id); else selected.Remove(level.id);
                GUILayout.Label(level.id.ToString(), GUILayout.Width(65)); GUILayout.Label(level.title ?? "", GUILayout.Width(190)); GUILayout.Label(level.difficulty ?? "", GUILayout.Width(65)); GUILayout.Label(level.profile ?? "", GUILayout.Width(65)); GUILayout.Label(level.cols+"x"+level.rows, GUILayout.Width(65)); GUILayout.Label((level.habitats?.Length??0).ToString(), GUILayout.Width(65)); GUILayout.Label((level.habitats??Array.Empty<HabitatDto>()).Sum(h=>h.stages==null||h.stages.Length==0?1:h.stages.Length).ToString(), GUILayout.Width(65)); GUILayout.Label((level.targets?.Length??0).ToString(), GUILayout.Width(65)); GUILayout.Label((level.elevators?.Length??0).ToString(), GUILayout.Width(65)); EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal(); GUI.enabled=selected.Count>0; if(GUILayout.Button("IMPORT SELECTED")) CompleteSelected(); if(GUILayout.Button("IMPORT ALL")){selected.Clear();foreach(var level in catalog.levels)selected.Add(level.id);CompleteSelected();} GUI.enabled=true; if(GUILayout.Button("CANCEL"))Close(); EditorGUILayout.EndHorizontal();
        }
        void CompleteSelected()
        {
            try { complete(selected.OrderBy(id=>id).Select(id=>R10Storage.ImportV5Catalog(path,id)).ToList()); Close(); }
            catch(Exception exception) { EditorUtility.DisplayDialog("Import V5 failed",exception.Message,"OK"); }
        }
    }
}
#endif