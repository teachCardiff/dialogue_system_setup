using System;
using UnityEngine;

[Serializable]
public class QuestOperation
{
    public enum OperationType
    {
        // Actions
        StartQuest,
        CompleteQuest,
        UpdateQuestProgress,

        // Quest status check
        CheckQuestStatus,

        // Generic variables (global or quest-scoped via name)
        SetInt,
        IncrementInt,
        CheckInt,
        SetBool,
        CheckBool
    }

    public enum ComparisonType { GreaterThan, LessThan, Equal, NotEqual }

    public OperationType operationType = OperationType.StartQuest;

    // Target quest (used for most quest-related operations)
    public Quest quest;

    // For UpdateQuestProgress
    public int objectiveIndex = 0;
    public int progressDelta = 1;

    // For int/bool operations and checks (replaces variableName)
    public VariableReference variable = new VariableReference();
    public int intValue = 0;
    public bool boolValue = true;
    public ComparisonType comparison = ComparisonType.Equal;

    // For CheckQuestStatus (reuse existing QuestConditionType semantics)
    public QuestConditionType requiredQuestStatus = QuestConditionType.Started;

    // Evaluate a check operation (returns true for actions that always succeed)
    public bool Evaluate(GameState gameState)
    {
        if (gameState == null) return false;

        switch (operationType)
        {
            case OperationType.CheckQuestStatus:
                if (quest == null)
                {
                    Debug.LogWarning("QuestOperation.Evaluate: CheckQuestStatus called with null quest.");
                    return false;
                }
                var q = gameState.GetQuest(quest.questName);
                bool isStarted = gameState.IsQuestStarted(quest.questName);
                bool isCompleted = gameState.IsQuestCompleted(quest.questName);
                switch (requiredQuestStatus)
                {
                    case QuestConditionType.NotStarted: return !isStarted;
                    case QuestConditionType.Started: return isStarted;
                    case QuestConditionType.InProgress: return q != null && q.currentStatus == Quest.Status.InProgress;
                    case QuestConditionType.Completed: return isCompleted;
                    default: return false;
                }

            case OperationType.CheckInt:
                {
                    var key = variable.ResolveKey();
                    if (string.IsNullOrEmpty(key)) return false;
                    int val = 0;
                    try { val = gameState.GetInt(key); } catch { }
                    switch (comparison)
                    {
                        case ComparisonType.GreaterThan: return val > intValue;
                        case ComparisonType.LessThan: return val < intValue;
                        case ComparisonType.Equal: return val == intValue;
                        case ComparisonType.NotEqual: return val != intValue;
                        default: return false;
                    }
                }

            case OperationType.CheckBool:
                {
                    var key = variable.ResolveKey();
                    if (string.IsNullOrEmpty(key)) return false;
                    bool b = false;
                    try { b = gameState.GetBool(key); } catch { }
                    return b == boolValue;
                }

            // For actions, treat as 'true' for evaluation purposes
            case OperationType.StartQuest:
            case OperationType.CompleteQuest:
            case OperationType.UpdateQuestProgress:
            case OperationType.SetInt:
            case OperationType.IncrementInt:
            case OperationType.SetBool:
                return true;

            default:
                return false;
        }
    }

    // Execute an operation (for consequences)
    public void Execute(GameState gameState)
    {
        if (gameState == null)
        {
            Debug.LogWarning("QuestOperation.Execute called with null GameState");
            return;
        }

        switch (operationType)
        {
            case OperationType.StartQuest:
                if (quest != null) gameState.StartQuest(quest);
                else Debug.LogError("QuestOperation.Execute StartQuest: quest is null.");
                break;
            case OperationType.CompleteQuest:
                if (quest != null) gameState.CompleteQuest(quest.questName);
                else Debug.LogError("QuestOperation.Execute CompleteQuest: quest is null.");
                break;
            case OperationType.UpdateQuestProgress:
                if (quest != null) gameState.UpdateQuestProgress(quest.questName, objectiveIndex, progressDelta);
                else Debug.LogError("QuestOperation.Execute UpdateQuestProgress: quest is null.");
                break;
            case OperationType.SetInt:
                {
                    var key = variable.ResolveKey();
                    if (!string.IsNullOrEmpty(key)) gameState.SetInt(key, intValue);
                    else Debug.LogWarning("QuestOperation.Execute SetInt: variable key empty.");
                    break;
                }
            case OperationType.IncrementInt:
                {
                    var key = variable.ResolveKey();
                    if (!string.IsNullOrEmpty(key)) gameState.SetInt(key, gameState.GetInt(key) + intValue);
                    else Debug.LogWarning("QuestOperation.Execute IncrementInt: variable key empty.");
                    break;
                }
            case OperationType.SetBool:
                {
                    var key = variable.ResolveKey();
                    if (!string.IsNullOrEmpty(key)) gameState.SetBool(key, boolValue);
                    else Debug.LogWarning("QuestOperation.Execute SetBool: variable key empty.");
                    break;
                }
            default:
                Debug.LogWarning($"QuestOperation.Execute: Unsupported operation type {operationType}");
                break;
        }
    }

    // Human readable summary for editor lists
    public string Summary()
    {
        try
        {
            switch (operationType)
            {
                case OperationType.StartQuest: return quest != null ? $"Start Quest: {quest.questName}" : "Start Quest: <none>";
                case OperationType.CompleteQuest: return quest != null ? $"Complete Quest: {quest.questName}" : "Complete Quest: <none>";
                case OperationType.UpdateQuestProgress: return quest != null ? $"Update Quest: {quest.questName} obj[{objectiveIndex}] += {progressDelta}" : "Update Quest: <none>";
                case OperationType.CheckQuestStatus: return quest != null ? $"Check Quest: {quest.questName} is {requiredQuestStatus}" : "Check Quest: <none>";
                case OperationType.SetInt: return $"Set Int: {variable}";
                case OperationType.IncrementInt: return $"Inc Int: {variable} += {intValue}";
                case OperationType.CheckInt: return $"Check Int: {variable} {comparison} {intValue}";
                case OperationType.SetBool: return $"Set Bool: {variable} = {boolValue}";
                case OperationType.CheckBool: return $"Check Bool: {variable} == {boolValue}";
                default: return operationType.ToString();
            }
        }
        catch { return operationType.ToString(); }
    }
}
