// filepath: Assets/DialogueSystem/Scripts/Editor/VariableRefDrawer.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VariableRef))]
public class VariableRefDrawer : PropertyDrawer
{
    private class Entry { public string id; public string display; public string path; }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var idProp = property.FindPropertyRelative("id");
        var pathProp = property.FindPropertyRelative("path");
        EditorGUI.BeginProperty(position, label, property);

        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var gs = FindGameState();
        if (gs == null || gs.root == null)
        {
            EditorGUI.HelpBox(line, "No GameState with Variables found.", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

        // Ensure IDs persist
        if (gs.root.EnsureAllIdsAssigned())
        {
            EditorUtility.SetDirty(gs);
            AssetDatabase.SaveAssets();
        }

        var entries = new List<Entry>();
        var pathParts = new List<string>();
        BuildEntriesFriendly(gs.root, entries, pathParts, isRoot:true);
        if (entries.Count == 0)
        {
            EditorGUI.HelpBox(line, "No variables in GameState.", MessageType.Info);
            EditorGUI.EndProperty();
            return;
        }

        // Auto-correct invalid id
        int currentIndex = entries.FindIndex(e => e.id == idProp.stringValue);
        if (currentIndex < 0)
        {
            idProp.stringValue = entries[0].id;
            pathProp.stringValue = entries[0].path;
            property.serializedObject.ApplyModifiedProperties();
            currentIndex = 0;
        }

        var labels = entries.Select(e => e.display).ToArray();
        int newIndex = EditorGUI.Popup(line, currentIndex, labels);
        if (newIndex != currentIndex && newIndex >= 0 && newIndex < entries.Count)
        {
            idProp.stringValue = entries[newIndex].id;
            pathProp.stringValue = entries[newIndex].path;
            property.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    private static void BuildEntriesFriendly(Variable node, List<Entry> list, List<string> path, bool isRoot)
    {
        if (node == null) return;

        if (!isRoot)
        {
            string label = GetDisplayLabel(node);

            bool isObjective = node is ObjectiveVariable;
            bool lastIsQuest = node.Parent is QuestVariable;
            if (isObjective && lastIsQuest)
            {
                if (path.Count == 0 || path[path.Count - 1] != "Objectives")
                {
                    path.Add("Objectives");
                }
            }

            path.Add(label);

            if (node.ValueType != null)
            {
                if (node is StringVar s && s.Parent is QuestVariable && string.Equals(s.Key, "name", System.StringComparison.OrdinalIgnoreCase)) { }
                else if (node is StringVar so && so.Parent is ObjectiveVariable && string.Equals(so.Key, "name", System.StringComparison.OrdinalIgnoreCase)) { }
                else
                {
                    list.Add(new Entry { id = node.Id, display = string.Join("/", path), path = node.GetPath() });
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            BuildEntriesFriendly(child, list, path, isRoot:false);
            if (path.Count > 0)
            {
                path.RemoveAt(path.Count - 1);
                if (path.Count > 0 && path[path.Count - 1] == "Objectives")
                {
                    path.RemoveAt(path.Count - 1);
                }
            }
        }
    }

    private static string GetDisplayLabel(Variable v)
    {
        if (v == null) return "(null)";
        if (v is QuestVariable q && q.name != null && !string.IsNullOrEmpty(q.name.value))
            return q.name.value;
        if (v is ObjectiveVariable ov && ov.name != null && !string.IsNullOrEmpty(ov.name.value))
            return ov.name.value;
        var label = string.IsNullOrEmpty(v.DisplayName) ? v.Key : v.DisplayName;
        return string.IsNullOrEmpty(label) ? v.GetType().Name : label;
    }

    private static GameState FindGameState()
    {
        DialogueManager mgr = null;
        #if UNITY_2023_1_OR_NEWER
        mgr = Object.FindFirstObjectByType<DialogueManager>();
        #else
        mgr = Object.FindObjectOfType<DialogueManager>();
        #endif
        if (mgr != null)
        {
            var fi = typeof(DialogueManager).GetField("gameState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null)
            {
                var gsFromMgr = fi.GetValue(mgr) as GameState;
                if (gsFromMgr != null) return gsFromMgr;
            }
        }
        var guids = AssetDatabase.FindAssets("t:GameState");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var gs = AssetDatabase.LoadAssetAtPath<GameState>(path);
            if (gs != null) return gs;
        }
        return null;
    }
}
