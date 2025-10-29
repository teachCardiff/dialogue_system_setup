// filepath: Assets/DialogueSystem/Scripts/Editor/VariableActionDrawer.cs
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(VariableAction))]
public class VariableActionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(line, property.FindPropertyRelative("kind"), GUIContent.none);
        line.y += EditorGUIUtility.singleLineHeight + 2;

        EditorGUI.PropertyField(line, property.FindPropertyRelative("variable"), GUIContent.none);
        line.y += EditorGUIUtility.singleLineHeight + 2;

        var kind = (ActionKind)property.FindPropertyRelative("kind").enumValueIndex;
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
            case ActionKind.SetQuestStatus:
                EditorGUI.PropertyField(line, property.FindPropertyRelative("enumString"), GUIContent.none);
                break;
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
        // add a small bottom padding
        return lines * (EditorGUIUtility.singleLineHeight + 2) + 2;
    }
}
