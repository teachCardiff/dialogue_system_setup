using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Migration utility to convert legacy QuestCondition / QuestConsequence ScriptableObjects
/// referenced from Dialogue assets into inline QuestOperation entries on DialogueChoice.
///
/// Usage: Window -> Dialogue -> Migrate Quest SOs
/// This will create a .bak copy of each Dialogue asset before modifying it.
/// The original SO assets are left intact.
/// </summary>
public static class MigrateQuestSOs
{
    [MenuItem("Tools/Dialogue/Migrate Quest SOs to Inline Operations")]
    public static void Migrate()
    {
        var guids = AssetDatabase.FindAssets("t:Dialogue");
        int total = guids.Length;
        if (total == 0)
        {
            EditorUtility.DisplayDialog("Migration", "No Dialogue assets found to migrate.", "OK");
            return;
        }

        int processed = 0;
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Migrating Dialogues", path, (float)i / total);

                var dialogue = AssetDatabase.LoadAssetAtPath<Dialogue>(path);
                if (dialogue == null) continue;

                // Backup the asset file once
                var backupPath = path + ".bak";
                if (!System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath.Replace("Assets", ""), backupPath)))
                {
                    AssetDatabase.CopyAsset(path, backupPath);
                }

                bool changed = false;

                // Iterate nodes
                foreach (var node in dialogue.nodes)
                {
                    if (node == null) continue;

                    // For each choice, convert conditions and consequences
                    foreach (var choice in node.choices)
                    {
                        if (choice == null) continue;

                        // Convert Conditions (QuestCondition, CheckIntCondition, CheckBoolConditionSO)
                        var toRemoveConds = new List<Condition>();

                        foreach (var cond in choice.conditions)
                        {
                            if (cond == null) continue;

                            // QuestCondition
                            if (cond is QuestCondition qc)
                            {
                                var op = new QuestOperation();
                                op.operationType = QuestOperation.OperationType.CheckQuestStatus;
                                op.quest = qc.quest;
                                op.requiredQuestStatus = qc.conditionType;

                                choice.operationConditions.Add(op);
                                toRemoveConds.Add(cond);
                                changed = true;
                                continue;
                            }

                            // CheckIntCondition
                            if (cond is CheckIntCondition cic)
                            {
                                var op = new QuestOperation();
                                op.operationType = QuestOperation.OperationType.CheckInt;
                                op = SetVariableGlobalKey(op, cic.variableName);
                                op.intValue = cic.compareValue;
                                op.comparison = (QuestOperation.ComparisonType)cic.comparison;

                                choice.operationConditions.Add(op);
                                toRemoveConds.Add(cond);
                                changed = true;
                                continue;
                            }

                            // CheckBoolConditionSO
                            if (cond is CheckBoolConditionSO cb)
                            {
                                var op = new QuestOperation();
                                op.operationType = QuestOperation.OperationType.CheckBool;
                                op = SetVariableGlobalKey(op, cb.variableName);
                                op.boolValue = cb.requiredValue;

                                choice.operationConditions.Add(op);
                                toRemoveConds.Add(cond);
                                changed = true;
                                continue;
                            }

                            // else: keep
                        }

                        // Remove converted condition SO references from the list
                        if (toRemoveConds.Count > 0)
                        {
                            foreach (var tr in toRemoveConds)
                            {
                                choice.conditions.Remove(tr);
                            }
                        }

                        // Convert Consequences (QuestConsequence)
                        var toRemoveCons = new List<Consequence>();
                        foreach (var cons in choice.consequences)
                        {
                            if (cons == null) continue;

                            if (cons is QuestConsequence qc2)
                            {
                                var op = new QuestOperation();
                                switch (qc2.consequenceType)
                                {
                                    case QuestConsequenceType.Start:
                                        op.operationType = QuestOperation.OperationType.StartQuest;
                                        op.quest = qc2.quest;
                                        break;
                                    case QuestConsequenceType.UpdateProgress:
                                        op.operationType = QuestOperation.OperationType.UpdateQuestProgress;
                                        op.quest = qc2.quest;
                                        op.objectiveIndex = qc2.objectiveIndex;
                                        op.progressDelta = qc2.progressDelta;
                                        break;
                                    case QuestConsequenceType.Complete:
                                        op.operationType = QuestOperation.OperationType.CompleteQuest;
                                        op.quest = qc2.quest;
                                        break;
                                }

                                choice.operationConsequences.Add(op);
                                toRemoveCons.Add(cons);
                                changed = true;
                            }
                        }

                        if (toRemoveCons.Count > 0)
                        {
                            foreach (var tr in toRemoveCons)
                            {
                                choice.consequences.Remove(tr);
                            }
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(dialogue);
                    AssetDatabase.SaveAssets();
                    processed++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog("Migration Complete", $"Processed {processed} dialogues (of {total}) and migrated found Quest SOs.", "OK");
        AssetDatabase.Refresh();
    }

    // Helper: set QuestOperation.variable to global scope with provided key using SerializedObject to avoid direct type coupling
    private class OpWrapperSO : ScriptableObject
    {
        public QuestOperation op = new QuestOperation();
    }

    private static QuestOperation SetVariableGlobalKey(QuestOperation source, string key)
    {
        if (source == null) source = new QuestOperation();
        var wrapper = ScriptableObject.CreateInstance<OpWrapperSO>();
        wrapper.op = source;
        var so = new SerializedObject(wrapper);
        var opProp = so.FindProperty("op");
        var varProp = opProp.FindPropertyRelative("variable");
        var keyProp = varProp.FindPropertyRelative("key");
        var scopeProp = varProp.FindPropertyRelative("scope");
        keyProp.stringValue = key;
        scopeProp.enumValueIndex = 0; // VariableReference.Scope.Global
        so.ApplyModifiedPropertiesWithoutUndo();
        // wrapper.op now contains updated data; return it
        return wrapper.op;
    }
}
