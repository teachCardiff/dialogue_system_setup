// filepath: Assets/DialogueSystem/Scripts/Editor/VariableOperationDrawer.cs
using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VariableOperation))]
public class VariableOperationDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // VariableRef
        var varProp = property.FindPropertyRelative("variable");
        EditorGUI.PropertyField(line, varProp, GUIContent.none);
        line.y += EditorGUIUtility.singleLineHeight + 2;

        // Resolve type via GameState
        System.Type valueType = null;
        var gs = FindGameState();
        if (gs != null)
        {
            var id = varProp.FindPropertyRelative("id").stringValue;
            var v = gs.TryResolveById(id);
            valueType = v?.ValueType;
        }
        else
        {
            EditorGUI.HelpBox(line, "No GameState asset found.", MessageType.Warning);
            EditorGUI.EndProperty();
            return;
        }

        if (valueType == typeof(int))
        {
            EditorGUI.PropertyField(line, property.FindPropertyRelative("numericOp"), GUIContent.none);
            line.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("intValue"), GUIContent.none);
        }
        else if (valueType == typeof(bool))
        {
            var opProp = property.FindPropertyRelative("boolOp");
            EditorGUI.PropertyField(line, opProp, GUIContent.none);
            var op = (BoolOperator)opProp.enumValueIndex;
            if (op == BoolOperator.Equal || op == BoolOperator.NotEqual)
            {
                line.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(line, property.FindPropertyRelative("boolValue"), GUIContent.none);
            }
        }
        else if (valueType == typeof(string))
        {
            EditorGUI.PropertyField(line, property.FindPropertyRelative("stringOp"), GUIContent.none);
            line.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("stringValue"), GUIContent.none);
        }
        else if (valueType != null && valueType.IsEnum)
        {
            EditorGUI.PropertyField(line, property.FindPropertyRelative("enumOp"), GUIContent.none);
            line.y += EditorGUIUtility.singleLineHeight + 2;

            // Dropdown for enumString
            var enumStringProp = property.FindPropertyRelative("enumString");
            DrawEnumStringDropdown(line, enumStringProp, valueType);
        }
        else
        {
            EditorGUI.HelpBox(line, "Select a variable to configure.", MessageType.Info);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = 0f;
        // variable line
        h += EditorGUIUtility.singleLineHeight + 2;
        // resolve type
        var varProp = property.FindPropertyRelative("variable");
        System.Type valueType = null;
        var gs = FindGameState();
        if (gs != null)
        {
            var id = varProp.FindPropertyRelative("id").stringValue;
            var v = gs.TryResolveById(id);
            valueType = v?.ValueType;
        }

        if (valueType == typeof(int)) h += (EditorGUIUtility.singleLineHeight + 2) * 2;
        else if (valueType == typeof(bool))
        {
            h += EditorGUIUtility.singleLineHeight + 2; // op
            var opProp = property.FindPropertyRelative("boolOp");
            var op = (BoolOperator)opProp.enumValueIndex;
            if (op == BoolOperator.Equal || op == BoolOperator.NotEqual)
                h += EditorGUIUtility.singleLineHeight + 2; // bool value
        }
        else if (valueType == typeof(string)) h += (EditorGUIUtility.singleLineHeight + 2) * 2;
        else if (valueType != null && valueType.IsEnum) h += (EditorGUIUtility.singleLineHeight + 2) * 2; // op + dropdown
        else h += EditorGUIUtility.singleLineHeight * 2;

        // small bottom padding
        return h + 2;
    }

    private static void DrawEnumStringDropdown(Rect line, SerializedProperty enumStringProp, System.Type enumType)
    {
        if (enumType == null || !enumType.IsEnum)
        {
            EditorGUI.PropertyField(line, enumStringProp, GUIContent.none);
            return;
        }
        var names = Enum.GetNames(enumType);
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
                if (string.Equals(names[i], enumStringProp.stringValue, StringComparison.OrdinalIgnoreCase))
                {
                    current = i; break;
                }
            }
        }
        // Friendly labels (insert spaces before capitals)
        string[] labels = new string[names.Length];
        for (int i = 0; i < names.Length; i++) labels[i] = PrettyEnumName(names[i]);

        int newIndex = EditorGUI.Popup(line, current, labels);
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

    private static GameState FindGameState()
    {
        // Prefer the GameState referenced by a DialogueManager in the open scene
        DialogueManager mgr = null;
        #if UNITY_2023_1_OR_NEWER
        mgr = UnityEngine.Object.FindFirstObjectByType<DialogueManager>();
        #else
        mgr = UnityEngine.Object.FindObjectOfType<DialogueManager>();
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
