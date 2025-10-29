#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dialogue Blackboard: a dockable editor window to quickly build QuestOperations
/// and apply them to the currently selected DialogueNode (enter/exit) or a choice
/// (conditions/consequences). Simplified to only show the Operation Builder and Preview.
/// </summary>
public class DialogueBlackboardWindow : EditorWindow
{
    // Operation builder working copy lives in a wrapper so we can use SerializedObject + PropertyDrawer
    [Serializable]
    private class OpWrapper : ScriptableObject
    {
        public QuestOperation op = new QuestOperation();
    }

    private OpWrapper wrapper;
    private SerializedObject serializedWrapper;

    // Targeting
    private enum TargetKind { NodeEnter, NodeExit, ChoiceCondition, ChoiceConsequence }
    private TargetKind target = TargetKind.NodeEnter;
    private int selectedChoiceIndex = 0;

    [MenuItem("Window/Dialogue/Blackboard")] 
    public static void Open()
    {
        var window = GetWindow<DialogueBlackboardWindow>(title: "Dialogue Blackboard");
        window.Show();
    }

    private void OnEnable()
    {
        if (wrapper == null)
        {
            wrapper = CreateInstance<OpWrapper>();
            wrapper.hideFlags = HideFlags.HideAndDontSave;
        }
        if (serializedWrapper == null)
        {
            serializedWrapper = new SerializedObject(wrapper);
        }
        Selection.selectionChanged += Repaint;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= Repaint;
        if (wrapper != null)
        {
            DestroyImmediate(wrapper);
            wrapper = null;
        }
        serializedWrapper = null;
    }

    private DialogueNode GetSelectedNode()
    {
        return Selection.activeObject as DialogueNode;
    }

    private void OnGUI()
    {
        DrawBuilderPane();
    }

    private void DrawBuilderPane()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            EditorGUILayout.LabelField("Operation Builder", EditorStyles.boldLabel);

            var node = GetSelectedNode();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Selected Node:", GUILayout.Width(100));
                EditorGUILayout.ObjectField(node, typeof(DialogueNode), false);
            }

            EditorGUILayout.Space(4);

            using (var scroll = new EditorGUILayout.ScrollViewScope(Vector2.zero))
            {
                // Drive UI through SerializedProperty so QuestOperationDrawer renders correctly
                if (serializedWrapper == null)
                    serializedWrapper = new SerializedObject(wrapper);

                serializedWrapper.Update();
                var opProp = serializedWrapper.FindProperty("op");
                EditorGUILayout.PropertyField(opProp, new GUIContent("Operation"), includeChildren: true);
                serializedWrapper.ApplyModifiedProperties();

                EditorGUILayout.Space(6);

                // Targeting
                target = (TargetKind)EditorGUILayout.EnumPopup("Apply To", target);

                if (target == TargetKind.ChoiceCondition || target == TargetKind.ChoiceConsequence)
                {
                    if (node != null && node.choices != null && node.choices.Count > 0)
                    {
                        var labels = node.choices.Select((c, i) => string.IsNullOrEmpty(c.choiceText) ? $"Choice {i}" : $"{i}: {c.choiceText}").ToArray();
                        selectedChoiceIndex = Mathf.Clamp(selectedChoiceIndex, 0, node.choices.Count - 1);
                        selectedChoiceIndex = EditorGUILayout.Popup("Choice", selectedChoiceIndex, labels);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Select a DialogueNode with choices to target a choice.", MessageType.Info);
                    }
                }

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = node != null;
                    if (GUILayout.Button("Add To Selected"))
                    {
                        ApplyToSelection(node);
                    }
                    GUI.enabled = true;

                    var dragRect = GUILayoutUtility.GetRect(120, 22);
                    GUI.Box(dragRect, "Drag Operation", EditorStyles.miniButton);
                    HandleDrag(dragRect);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(wrapper != null ? wrapper.op.Summary() : "<no op>", MessageType.None);
        }
    }

    private void ApplyToSelection(DialogueNode node)
    {
        if (node == null)
        {
            ShowNotification(new GUIContent("Select a DialogueNode in the Hierarchy/Project/Graph"));
            return;
        }

        Undo.RecordObject(node, "Add Quest Operation");

        var copy = DeepCopy(wrapper.op);
        switch (target)
        {
            case TargetKind.NodeEnter:
                node.enterOperations.Add(copy);
                break;
            case TargetKind.NodeExit:
                node.exitOperations.Add(copy);
                break;
            case TargetKind.ChoiceCondition:
                if (node.choices != null && node.choices.Count > 0 && selectedChoiceIndex >= 0 && selectedChoiceIndex < node.choices.Count)
                {
                    node.choices[selectedChoiceIndex].operationConditions.Add(copy);
                }
                else
                {
                    Debug.LogWarning("Blackboard: No valid choice to add condition.");
                }
                break;
            case TargetKind.ChoiceConsequence:
                if (node.choices != null && node.choices.Count > 0 && selectedChoiceIndex >= 0 && selectedChoiceIndex < node.choices.Count)
                {
                    node.choices[selectedChoiceIndex].operationConsequences.Add(copy);
                }
                else
                {
                    Debug.LogWarning("Blackboard: No valid choice to add consequence.");
                }
                break;
        }

        EditorUtility.SetDirty(node);
        AssetDatabase.SaveAssets();
        Repaint();

        Debug.Log($"[Blackboard] Added operation to {node.name}: {copy.Summary()}");
    }

    private static QuestOperation DeepCopy(QuestOperation src)
    {
        if (src == null) return null;
        try
        {
            var json = JsonUtility.ToJson(src);
            return JsonUtility.FromJson<QuestOperation>(json);
        }
        catch
        {
            // Fallback to manual copy if needed (extend as fields evolve)
            return new QuestOperation
            {
                operationType = src.operationType,
                quest = src.quest,
                objectiveIndex = src.objectiveIndex,
                progressDelta = src.progressDelta,
                // variable field intentionally omitted in fallback to avoid type ref; JSON path covers normal usage
                intValue = src.intValue,
                boolValue = src.boolValue,
                comparison = src.comparison,
                requiredQuestStatus = src.requiredQuestStatus
            };
        }
    }

    private void HandleDrag(Rect dragRect)
    {
        var e = Event.current;
        if (!dragRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            DragAndDrop.PrepareStartDrag();
            var payload = ScriptableObject.CreateInstance<QuestOperationDragPayload>();
            payload.operationJson = JsonUtility.ToJson(wrapper.op);
            DragAndDrop.objectReferences = new UnityEngine.Object[] { payload };
            DragAndDrop.SetGenericData("QuestOperationJson", payload.operationJson);
            DragAndDrop.StartDrag("QuestOperation");
            e.Use();
        }
    }

    private class QuestOperationDragPayload : ScriptableObject
    {
        public string operationJson;
    }
}
#endif
