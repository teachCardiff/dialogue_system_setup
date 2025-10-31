// filepath: Assets/DialogueSystem/Scripts/Editor/VariableActionDrawer.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VariableAction))]
public class VariableActionDrawer : PropertyDrawer
{
    private class Entry { public string id; public string display; }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // Kind first
        var kindProp = property.FindPropertyRelative("kind");
        EditorGUI.PropertyField(line, kindProp, GUIContent.none);
        line.y += EditorGUIUtility.singleLineHeight + 2;

        // Filtered variable picker
        var varProp = property.FindPropertyRelative("variable");
        var idProp = varProp.FindPropertyRelative("id");
        var kind = (ActionKind)kindProp.enumValueIndex;

        var gs = FindGameState();
        if (gs == null || gs.root == null)
        {
            EditorGUI.HelpBox(line, "No GameState with Variables found.", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

        var entries = new List<Entry>();
        BuildEntriesForKind(gs.root, entries, new List<string>(), kind, isRoot: true);

        if (entries.Count == 0)
        {
            EditorGUI.HelpBox(line, "No compatible variables found for this action.", MessageType.Info);
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
        line.y += EditorGUIUtility.singleLineHeight + 2;

        // Payload by kind
        switch (kind)
        {
            case ActionKind.SetInt:
            case ActionKind.IncInt:
                EditorGUI.PropertyField(line, property.FindPropertyRelative("intValue"), GUIContent.none);
                break;
            case ActionKind.SetBool:
                EditorGUI.PropertyField(line, property.FindPropertyRelative("boolValue"), GUIContent.none);
                break;
            case ActionKind.ToggleBool:
                EditorGUI.LabelField(line, "Toggle (no extra value)");
                break;
            case ActionKind.SetString:
                EditorGUI.PropertyField(line, property.FindPropertyRelative("stringValue"), GUIContent.none);
                break;
            case ActionKind.SetEnum:
            {
                // Determine enum type from selected variable id
                var id = idProp.stringValue;
                System.Type enumType = null;
                if (!string.IsNullOrEmpty(id))
                {
                    var v = gs.TryResolveById(id);
                    enumType = v?.ValueType;
                }
                var enumStringProp = property.FindPropertyRelative("enumString");
                DrawEnumStringDropdown(line, enumStringProp, enumType);
                break;
            }
            case ActionKind.SetQuestStatus:
            {
                // Always use QuestStatus enum list
                var enumStringProp = property.FindPropertyRelative("enumString");
                DrawEnumStringDropdown(line, enumStringProp, typeof(QuestStatus));
                break;
            }
            case ActionKind.SetObjectiveProgress:
                EditorGUI.PropertyField(line, property.FindPropertyRelative("objectiveIndex"), new GUIContent("Objective Index"));
                line.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("intValue"), new GUIContent("Progress"));
                break;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var kind = (ActionKind)property.FindPropertyRelative("kind").enumValueIndex;
        int lines = 2; // kind + variable
        switch (kind)
        {
            case ActionKind.SetInt:
            case ActionKind.IncInt:
            case ActionKind.SetBool:
            case ActionKind.SetString:
            case ActionKind.SetEnum:
            case ActionKind.SetQuestStatus:
                lines += 1; break;
            case ActionKind.ToggleBool:
                lines += 1; break;
            case ActionKind.SetObjectiveProgress:
                lines += 2; break;
        }
        return lines * (EditorGUIUtility.singleLineHeight + 2) + 2;
    }

    private static void BuildEntriesForKind(Variable node, List<Entry> list, List<string> path, ActionKind kind, bool isRoot)
    {
        if (node == null) return;

        // add to path (skip root visual)
        if (!isRoot)
        {
            string label = GetDisplayLabel(node);

            // Virtual group name between quest and objectives
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

            // Decide if node is selectable for this action kind
            if (IsSelectableForKind(node, kind))
            {
                // Exclude internal quest/objective name fields
                if (node is StringVar s && s.Parent is QuestVariable && string.Equals(s.Key, "name", System.StringComparison.OrdinalIgnoreCase)) { }
                else if (node is StringVar so && so.Parent is ObjectiveVariable && string.Equals(so.Key, "name", System.StringComparison.OrdinalIgnoreCase)) { }
                else
                {
                    list.Add(new Entry { id = node.Id, display = string.Join("/", path) });
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            BuildEntriesForKind(child, list, path, kind, isRoot: false);
            if (path.Count > 0)
            {
                path.RemoveAt(path.Count - 1);
                if (path.Count > 0 && path[path.Count - 1] == "Objectives")
                {
                    path.RemoveAt(path.Count - 1);
                }
            }
        }

        // Also allow selecting the Quest container itself for SetObjectiveProgress and SetQuestStatus
        if (isRoot)
        {
            if (kind == ActionKind.SetObjectiveProgress || kind == ActionKind.SetQuestStatus)
            {
                AddQuestContainers(node, list, new List<string>(), isRoot: true);
            }
        }
    }

    private static void AddQuestContainers(Variable node, List<Entry> list, List<string> path, bool isRoot)
    {
        if (node == null) return;
        if (!isRoot)
        {
            string label = GetDisplayLabel(node);
            path.Add(label);
            if (node is QuestVariable)
            {
                list.Add(new Entry { id = node.Id, display = string.Join("/", path) });
            }
        }
        foreach (var child in node.GetChildren())
        {
            AddQuestContainers(child, list, path, isRoot: false);
            if (path.Count > 0)
            {
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    private static bool IsSelectableForKind(Variable node, ActionKind kind)
    {
        // Typed leaves only, unless special-cased quest containers
        var t = node.ValueType;
        switch (kind)
        {
            case ActionKind.SetInt:
            case ActionKind.IncInt:
                return t == typeof(int);
            case ActionKind.SetBool:
            case ActionKind.ToggleBool:
                return t == typeof(bool);
            case ActionKind.SetString:
                return t == typeof(string);
            case ActionKind.SetEnum:
                return t != null && t.IsEnum;
            case ActionKind.SetQuestStatus:
                // quest container only; handled by AddQuestContainers
                return false;
            case ActionKind.SetObjectiveProgress:
                // quest container only; handled by AddQuestContainers
                return false;
        }
        return false;
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

    private static void DrawEnumStringDropdown(Rect line, SerializedProperty enumStringProp, System.Type enumType)
    {
        if (enumType == null || !enumType.IsEnum)
        {
            EditorGUI.PropertyField(line, enumStringProp, GUIContent.none);
            return;
        }
        var names = System.Enum.GetNames(enumType);
        if (names == null || names.Length == 0)
        {
            EditorGUI.PropertyField(line, enumStringProp, GUIContent.none);
            return;
        }
        int current = 0;
        if (!string.IsNullOrEmpty(enumStringProp.stringValue))
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], enumStringProp.stringValue, System.StringComparison.OrdinalIgnoreCase))
                {
                    current = i; break;
                }
            }
        }
        string[] labels2 = new string[names.Length];
        for (int i = 0; i < names.Length; i++) labels2[i] = PrettyEnumName(names[i]);
        int newIndex = EditorGUI.Popup(line, current, labels2);
        if (newIndex != current)
        {
            enumStringProp.stringValue = names[newIndex];
            enumStringProp.serializedObject.ApplyModifiedProperties();
        }
    }

    private static string PrettyEnumName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(raw[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
