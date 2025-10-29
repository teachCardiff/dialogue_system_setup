// filepath: Assets/DialogueSystem/Scripts/Editor/DialogueNodeInspector.cs
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

[CustomEditor(typeof(DialogueNode))]
public class DialogueNodeInspector : Editor
{
    SerializedProperty speakerCharacter;
    SerializedProperty speakerExpression;
    SerializedProperty listenerCharacter;
    SerializedProperty listenerExpression;
    SerializedProperty listenerIsSpeaker;
    SerializedProperty speakerName;
    SerializedProperty dialogueText;

    SerializedProperty conditionalBranches;
    SerializedProperty choices;
    SerializedProperty exitActions;

    ReorderableList branchList;
    ReorderableList choiceList;

    private void OnEnable()
    {
        speakerCharacter = serializedObject.FindProperty("speakerCharacter");
        speakerExpression = serializedObject.FindProperty("speakerExpression");
        listenerCharacter = serializedObject.FindProperty("listenerCharacter");
        listenerExpression = serializedObject.FindProperty("listenerExpression");
        listenerIsSpeaker = serializedObject.FindProperty("listenerIsSpeaker");
        speakerName = serializedObject.FindProperty("speakerName");
        dialogueText = serializedObject.FindProperty("dialogueText");

        conditionalBranches = serializedObject.FindProperty("conditionalBranches");
        choices = serializedObject.FindProperty("choices");
        exitActions = serializedObject.FindProperty("exitActions");

        // Branch list
        branchList = new ReorderableList(serializedObject, conditionalBranches, true, true, true, true);
        branchList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Conditional Branching");
        branchList.elementHeightCallback = index =>
        {
            var el = conditionalBranches.GetArrayElementAtIndex(index);
            float h = EditorGUIUtility.singleLineHeight * 4 + 12; // approx for name + operations + target
            var ops = el.FindPropertyRelative("operations");
            h += Mathf.Max(EditorGUIUtility.singleLineHeight + 6, EditorGUI.GetPropertyHeight(ops));
            return h;
        };
        branchList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var el = conditionalBranches.GetArrayElementAtIndex(index);
            var nameProp = el.FindPropertyRelative("branchName");
            var opsProp = el.FindPropertyRelative("operations");
            var targetProp = el.FindPropertyRelative("targetNode");

            var line = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, nameProp);
            line.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(line, opsProp, new GUIContent("Operations"), true);
            line.y += EditorGUI.GetPropertyHeight(opsProp) + 2;
            EditorGUI.PropertyField(line, targetProp);
        };

        // Choice list
        choiceList = new ReorderableList(serializedObject, choices, true, true, true, true);
        choiceList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Dialogue Choices");
        choiceList.elementHeightCallback = index =>
        {
            var el = choices.GetArrayElementAtIndex(index);
            float h = EditorGUIUtility.singleLineHeight * 5 + 12; // choice text, showIf, target, criteria, consequences
            var crit = el.FindPropertyRelative("criteria");
            var cons = el.FindPropertyRelative("consequences");
            h += EditorGUI.GetPropertyHeight(crit) + EditorGUI.GetPropertyHeight(cons);
            return h;
        };
        choiceList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var el = choices.GetArrayElementAtIndex(index);
            var textProp = el.FindPropertyRelative("choiceText");
            var targetProp = el.FindPropertyRelative("targetNode");
            var showIf = el.FindPropertyRelative("showIfCriteriaNotMet");
            var criteriaProp = el.FindPropertyRelative("criteria");
            var consProp = el.FindPropertyRelative("consequences");

            var line = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, textProp);
            line.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(line, targetProp);
            line.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(line, showIf, new GUIContent("Show if criteria not met"));
            line.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(line, criteriaProp, new GUIContent("Criteria"), true);
            line.y += EditorGUI.GetPropertyHeight(criteriaProp) + 2;
            EditorGUI.PropertyField(line, consProp, new GUIContent("Consequences"), true);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Actors
        EditorGUILayout.LabelField("Actors", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(speakerCharacter);
        if (speakerCharacter.objectReferenceValue != null)
        {
            EditorGUILayout.PropertyField(speakerExpression);
            // Mirror name with character when set
            var character = speakerCharacter.objectReferenceValue as Character;
            if (character != null)
            {
                speakerName.stringValue = character.npcName;
            }
        }
        EditorGUILayout.PropertyField(listenerCharacter);
        if (listenerCharacter.objectReferenceValue != null)
        {
            EditorGUILayout.PropertyField(listenerExpression);
        }
        EditorGUILayout.PropertyField(listenerIsSpeaker);
        EditorGUILayout.PropertyField(dialogueText);

        EditorGUILayout.Space();
        branchList.DoLayoutList();

        EditorGUILayout.Space();
        choiceList.DoLayoutList();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Consequences", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(exitActions, includeChildren: true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onEnterNode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("onExitNode"));

        serializedObject.ApplyModifiedProperties();
    }
}
