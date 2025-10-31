// filepath: Assets/DialogueSystem/Scripts/Editor/VariableOperationDrawer.cs
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
            EditorGUI.PropertyField(line, property.FindPropertyRelative("enumString"), GUIContent.none);
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
        else if (valueType != null && valueType.IsEnum) h += (EditorGUIUtility.singleLineHeight + 2) * 2;
        else h += EditorGUIUtility.singleLineHeight * 2;

        // small bottom padding
        return h + 2;
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
