using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(DialogueNode), true)]
public class DialogueNodeEditor : Editor
{
    private ReorderableList enterOpsList;
    private ReorderableList exitOpsList;

    private void OnEnable()
    {
        var node = target as DialogueNode;
        if (node == null) return;

        serializedObject.Update();

        var enterProp = serializedObject.FindProperty("enterOperations");
        var exitProp = serializedObject.FindProperty("exitOperations");

        if (enterProp != null)
        {
            enterOpsList = new ReorderableList(serializedObject, enterProp, true, true, true, true);
            enterOpsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Enter Operations");
            enterOpsList.elementHeightCallback = index => EditorGUI.GetPropertyHeight(enterProp.GetArrayElementAtIndex(index), true) + 6;
            enterOpsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 2;
                var elem = enterProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, elem, GUIContent.none, includeChildren: true);
            };
        }

        if (exitProp != null)
        {
            exitOpsList = new ReorderableList(serializedObject, exitProp, true, true, true, true);
            exitOpsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Exit Operations");
            exitOpsList.elementHeightCallback = index => EditorGUI.GetPropertyHeight(exitProp.GetArrayElementAtIndex(index), true) + 6;
            exitOpsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 2;
                var elem = exitProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, elem, GUIContent.none, includeChildren: true);
            };
        }

        serializedObject.ApplyModifiedProperties();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        if (enterOpsList != null)
        {
            GUILayout.Space(8);
            enterOpsList.DoLayoutList();
        }
        if (exitOpsList != null)
        {
            GUILayout.Space(8);
            exitOpsList.DoLayoutList();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
