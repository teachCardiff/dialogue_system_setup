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
        BuildEntries(gs.root, entries);
        if (entries.Count == 0)
        {
            EditorGUI.HelpBox(line, "No variables in GameState.root.", MessageType.Info);
            line.y += EditorGUIUtility.singleLineHeight + 4;
            if (GUI.Button(line, "+ Create Int"))
            {
                var ints = gs.root.EnsureGroup("Legacy", "Ints");
                var v = new IntVar { Key = "NewInt", DisplayName = "New Int" };
                ints.AddChild(v);
                gs.onStateChanged?.Invoke();
                EditorUtility.SetDirty(gs);
                AssetDatabase.SaveAssets();
            }
            EditorGUI.EndProperty();
            return;
        }

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

    private static void BuildEntries(Variable root, List<Entry> list)
    {
        foreach (var v in VariableGroup.Traverse(root))
        {
            if (v == null) continue;
            // Skip the root VariableGroup itself
            if (v == root) continue;
            // Include QuestVariable as selectable
            if (v is QuestVariable qv)
            {
                list.Add(new Entry { id = v.Id, display = TrimRoot(qv.GetPath()) });
                continue;
            }
            // Include primitives and enums (have a concrete ValueType)
            if (v.ValueType != null)
            {
                list.Add(new Entry { id = v.Id, display = TrimRoot(v.GetPath()) });
            }
        }
        // Sort alphabetically by display path
        list.Sort((a, b) => string.Compare(a.display, b.display, System.StringComparison.OrdinalIgnoreCase));
    }

    private static string TrimRoot(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        // Remove leading "Root/" if present
        if (path.StartsWith("Root/")) return path.Substring(5);
        if (path == "Root") return string.Empty;
        return path;
    }

    private static GameState FindGameState()
    {
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
