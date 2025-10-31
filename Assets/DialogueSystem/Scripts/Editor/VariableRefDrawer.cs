// filepath: Assets/DialogueSystem/Scripts/Editor/VariableRefDrawer.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VariableRef))]
public class VariableRefDrawer : PropertyDrawer
{
    private class Entry { public string id; public string display; }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var idProp = property.FindPropertyRelative("id");
        EditorGUI.BeginProperty(position, label, property);

        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var gs = FindGameState();
        if (gs == null || gs.root == null)
        {
            EditorGUI.HelpBox(line, "No GameState with Variables found.", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

        var entries = new List<Entry>();
        var path = new List<string>();
        BuildEntriesFriendly(gs.root, entries, path, isRoot:true);
        if (entries.Count == 0)
        {
            EditorGUI.HelpBox(line, "No variables in GameState.", MessageType.Info);
            EditorGUI.EndProperty();
            return;
        }

        // Preserve authoring order (no sort) so it matches GameState tree
        // entries.Sort((a, b) => string.Compare(a.display, b.display, System.StringComparison.OrdinalIgnoreCase));

        int currentIndex = Mathf.Max(0, entries.FindIndex(e => e.id == idProp.stringValue));
        var labels = entries.Select(e => e.display).ToArray();
        int newIndex = EditorGUI.Popup(line, currentIndex, labels);
        if (newIndex != currentIndex && newIndex >= 0 && newIndex < entries.Count)
        {
            idProp.stringValue = entries[newIndex].id;
            property.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Default to 1 line. Drawer callers must respect GetPropertyHeight
        return EditorGUIUtility.singleLineHeight;
    }

    private static void BuildEntriesFriendly(Variable node, List<Entry> list, List<string> path, bool isRoot)
    {
        if (node == null) return;

        // Skip adding the root itself to the path
        if (!isRoot)
        {
            string label = GetDisplayLabel(node);

            // Insert a virtual "Objectives" group name between a Quest and its Objective children for readability
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

            // Add selectable entries only for typed leaves (and enums), skip composite-only nodes
            if (node.ValueType != null)
            {
                // Skip quest.name and objective.name internal fields
                if (node is StringVar s && s.Parent is QuestVariable && string.Equals(s.Key, "name", System.StringComparison.OrdinalIgnoreCase))
                {
                    // do nothing
                }
                else if (node is StringVar so && so.Parent is ObjectiveVariable && string.Equals(so.Key, "name", System.StringComparison.OrdinalIgnoreCase))
                {
                    // do nothing
                }
                else
                {
                    list.Add(new Entry { id = node.Id, display = string.Join("/", path) });
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
        // Prefer the GameState referenced by a DialogueManager in the open scene
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
        // Fallback to first GameState asset
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
