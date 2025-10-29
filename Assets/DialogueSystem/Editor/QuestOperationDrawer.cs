using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(QuestOperation))]
public class QuestOperationDrawer : PropertyDrawer
{
    const int lineHeight = 18;
    const int padding = 2;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var opType = property.FindPropertyRelative("operationType");
        var quest = property.FindPropertyRelative("quest");
        var variable = property.FindPropertyRelative("variable");
        int lines = 1; // always draw opType first

        var type = (QuestOperation.OperationType)opType.enumValueIndex;

        switch (type)
        {
            case QuestOperation.OperationType.StartQuest:
            case QuestOperation.OperationType.CompleteQuest:
                lines += 1; // quest
                break;
            case QuestOperation.OperationType.CheckQuestStatus:
                lines += 2; // quest + required status
                break;
            case QuestOperation.OperationType.UpdateQuestProgress:
                lines += 3; // quest + objective + delta
                break;
            case QuestOperation.OperationType.SetInt:
            case QuestOperation.OperationType.IncrementInt:
                lines += GetVariableReferenceLineCount(variable); // variable ref
                lines += 1; // intValue
                break;
            case QuestOperation.OperationType.CheckInt:
                lines += GetVariableReferenceLineCount(variable);
                lines += 2; // intValue + comparison
                break;
            case QuestOperation.OperationType.SetBool:
                lines += GetVariableReferenceLineCount(variable);
                lines += 1; // boolValue
                break;
            case QuestOperation.OperationType.CheckBool:
                lines += GetVariableReferenceLineCount(variable);
                lines += 1; // boolValue
                break;
            default:
                break;
        }

        return lines * lineHeight + padding * 2;
    }

    private int GetVariableReferenceLineCount(SerializedProperty variableProp)
    {
        if (variableProp == null) return 2; // scope + key fallback
        var scopeProp = variableProp.FindPropertyRelative("scope");
        bool questScoped = scopeProp != null && scopeProp.enumValueIndex == 1; // 0=Global,1=QuestScoped
        // scope + (optional quest) + key
        return questScoped ? 3 : 2;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var opType = property.FindPropertyRelative("operationType");
        var quest = property.FindPropertyRelative("quest");
        var objectiveIndex = property.FindPropertyRelative("objectiveIndex");
        var progressDelta = property.FindPropertyRelative("progressDelta");
        var variable = property.FindPropertyRelative("variable");
        var intValue = property.FindPropertyRelative("intValue");
        var boolValue = property.FindPropertyRelative("boolValue");
        var comparison = property.FindPropertyRelative("comparison");
        var requiredQuestStatus = property.FindPropertyRelative("requiredQuestStatus");

        Rect r = new Rect(position.x, position.y + padding, position.width, lineHeight);
        EditorGUI.PropertyField(r, opType);

        var type = (QuestOperation.OperationType)opType.enumValueIndex;

        r.y += lineHeight + padding;

        switch (type)
        {
            case QuestOperation.OperationType.StartQuest:
            case QuestOperation.OperationType.CompleteQuest:
            case QuestOperation.OperationType.CheckQuestStatus:
                EditorGUI.PropertyField(r, quest);
                r.y += lineHeight + padding;
                if (type == QuestOperation.OperationType.CheckQuestStatus)
                {
                    EditorGUI.PropertyField(r, requiredQuestStatus);
                }
                break;
            case QuestOperation.OperationType.UpdateQuestProgress:
                EditorGUI.PropertyField(r, quest);
                r.y += lineHeight + padding;
                EditorGUI.PropertyField(r, objectiveIndex);
                r.y += lineHeight + padding;
                EditorGUI.PropertyField(r, progressDelta);
                break;
            case QuestOperation.OperationType.SetInt:
            case QuestOperation.OperationType.IncrementInt:
            case QuestOperation.OperationType.CheckInt:
                DrawVariableReference(ref r, variable);
                EditorGUI.PropertyField(r, intValue);
                r.y += lineHeight + padding;
                if (type == QuestOperation.OperationType.CheckInt)
                    EditorGUI.PropertyField(r, comparison);
                break;
            case QuestOperation.OperationType.SetBool:
            case QuestOperation.OperationType.CheckBool:
                DrawVariableReference(ref r, variable);
                EditorGUI.PropertyField(r, boolValue);
                break;
            default:
                EditorGUI.LabelField(r, "Unsupported operation");
                break;
        }

        EditorGUI.EndProperty();
    }

    private void DrawVariableReference(ref Rect r, SerializedProperty variableProp)
    {
        var scopeProp = variableProp.FindPropertyRelative("scope");
        var keyProp = variableProp.FindPropertyRelative("key");
        var questProp = variableProp.FindPropertyRelative("quest");

        EditorGUI.PropertyField(r, scopeProp);
        r.y += lineHeight + padding;
        if (scopeProp.enumValueIndex == 1) // QuestScoped
        {
            EditorGUI.PropertyField(r, questProp);
            r.y += lineHeight + padding;
        }
        EditorGUI.PropertyField(r, keyProp, new GUIContent("Variable Key"));
        r.y += lineHeight + padding;
    }
}
