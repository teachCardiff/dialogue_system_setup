// filepath: Assets/DialogueSystem/Scripts/Editor/VariableActionDrawer.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VariableAction))]
public class VariableActionDrawer : PropertyDrawer
{
    private class Entry { public string id; public string display; public string path; }

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
        var pathProp = varProp.FindPropertyRelative("path");
        var kind = (ActionKind)kindProp.enumValueIndex;

        var gs = FindGameState();
        if (gs == null || gs.root == null)
        {
            EditorGUI.HelpBox(line, "No GameState with Variables found.", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

        // NEW: ensure and persist IDs so action selections remain valid at runtime
        if (gs.root.EnsureAllIdsAssigned())
        {
            EditorUtility.SetDirty(gs);
            AssetDatabase.SaveAssets();
        }

        var entries = new List<Entry>();
        BuildEntriesForKind(gs.root, entries, new List<string>(), kind, isRoot: true);

        if (entries.Count == 0)
        {
            EditorGUI.HelpBox(line, "No compatible variables found for this action.", MessageType.Info);
            EditorGUI.EndProperty();
            return;
        }

        // Auto-correct invalid IDs that are not present under this kind's filter
        int selectedIndex = entries.FindIndex(e => e.id == idProp.stringValue);
        if (selectedIndex < 0)
        {
            idProp.stringValue = entries[0].id;
            pathProp.stringValue = entries[0].path; // store human path; runtime uses node.GetPath() fallback where we assign below
            property.serializedObject.ApplyModifiedProperties();
            selectedIndex = 0;
        }

        var labels = entries.Select(e => e.display).ToArray();
        int newIndex = EditorGUI.Popup(line, selectedIndex, labels);
        if (newIndex != selectedIndex && newIndex >= 0 && newIndex < entries.Count)
        {
            idProp.stringValue = entries[newIndex].id;
            pathProp.stringValue = entries[newIndex].path; // display equals human path
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
            {
                DrawObjectivePopupOrInfo(line, property, varProp, gs);
                line.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("intValue"), new GUIContent("Progress"));
                break;
            }
            case ActionKind.ModifyObjectiveProgress:
            {
                DrawObjectivePopupOrInfo(line, property, varProp, gs);
                line.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("arithmeticOp"), new GUIContent("Operation"));
                line.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("intValue"), new GUIContent("Amount"));
                line.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("clampToTargetRange"), new GUIContent("Clamp 0..Target"));
                break;
            }
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
            case ActionKind.ModifyObjectiveProgress:
                lines += 4; break;
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
                    list.Add(new Entry { id = node.Id, display = string.Join("/", path), path = node.GetPath() });
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

        // Include quest containers for SetQuestStatus and SetObjectiveProgress
        if (isRoot)
        {
            if (kind == ActionKind.SetObjectiveProgress || kind == ActionKind.SetQuestStatus || kind == ActionKind.ModifyObjectiveProgress)
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
                list.Add(new Entry { id = node.Id, display = string.Join("/", path), path = node.GetPath() });
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
                return false;
            case ActionKind.SetObjectiveProgress:
                return false;
            case ActionKind.ModifyObjectiveProgress:
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
        bool matched = false;
        if (!string.IsNullOrEmpty(enumStringProp.stringValue))
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], enumStringProp.stringValue, System.StringComparison.OrdinalIgnoreCase))
                {
                    current = i; matched = true; break;
                }
            }
        }
        if (!matched)
        {
            enumStringProp.stringValue = names[current];
            enumStringProp.serializedObject.ApplyModifiedProperties();
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

    // --- New helpers for Objective dropdowns ---

    private static Variable ResolveWithFallback(GameState gs, string id, string path)
    {
        if (gs == null) return null;
        Variable v = null;
        if (!string.IsNullOrEmpty(id)) v = gs.TryResolveById(id);
        if (v == null && !string.IsNullOrEmpty(path)) v = gs.root?.FindByPath(path);
        return v;
    }

    private static void DrawObjectivePopupOrInfo(Rect line, SerializedProperty actionProperty, SerializedProperty varProp, GameState gs)
    {
        var id = varProp.FindPropertyRelative("id").stringValue;
        var path = varProp.FindPropertyRelative("path").stringValue;
        var v = ResolveWithFallback(gs, id, path);
        var idxProp = actionProperty.FindPropertyRelative("objectiveIndex");

        if (v is QuestVariable qv && qv.objectives != null && qv.objectives.Count > 0)
        {
            // Build labels from objective names, fallback to display/key
            var labels = new string[qv.objectives.Count];
            for (int i = 0; i < qv.objectives.Count; i++)
            {
                labels[i] = BuildObjectiveLabel(qv, qv.objectives[i], i);
            }
            int current = Mathf.Clamp(idxProp.intValue, 0, labels.Length - 1);
            // Draw label + popup field
            float lw = EditorGUIUtility.labelWidth;
            var labelRect = new Rect(line.x, line.y, lw, line.height);
            var fieldRect = new Rect(line.x + lw, line.y, line.width - lw, line.height);
            EditorGUI.LabelField(labelRect, "Objective");
            int newIndex = EditorGUI.Popup(fieldRect, current, labels);
            if (newIndex != current)
            {
                idxProp.intValue = newIndex;
                actionProperty.serializedObject.ApplyModifiedProperties();
            }
            else if (current != idxProp.intValue)
            {
                // clamp applied
                idxProp.intValue = current;
                actionProperty.serializedObject.ApplyModifiedProperties();
            }
        }
        else
        {
            // No quest or no objectives
            EditorGUI.HelpBox(line, "Select a Quest with Objectives.", MessageType.Info);
        }
    }

    private static string BuildObjectiveLabel(QuestVariable quest, ObjectiveVariable obj, int index)
    {
        if (obj == null) return $"Objective {index + 1}";
        string name = obj.name != null ? obj.name.value : null;
        if (string.IsNullOrEmpty(name)) name = obj.Display;
        if (string.IsNullOrEmpty(name)) name = obj.DisplayName;
        if (string.IsNullOrEmpty(name)) name = obj.Key;
        if (string.IsNullOrEmpty(name)) name = $"Objective {index + 1}";
        // include index for clarity
        return $"{index + 1}. {name}";
    }
}
