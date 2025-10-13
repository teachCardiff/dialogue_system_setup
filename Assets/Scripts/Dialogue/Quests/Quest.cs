using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Quest", menuName = "Dialogue/Quest", order = 5)]
public class Quest : ScriptableObject
{
    [Header("Quest Info")]
    public string questName;
    [TextArea(2, 5)] public string description;

    [Header("Objectives")]
    public List<QuestObjective> objectives = new List<QuestObjective>();

    // Quest status: NotStarted, InProgress, Completed
    public enum Status { NotStarted, InProgress, Completed }
    [HideInInspector] public Status currentStatus = Status.NotStarted;

    // Progress tracking (updated via consequences)
    public int GetObjectiveProgress(int objectiveIndex)
    {
        return objectives[objectiveIndex].currentProgress;
    }

    public void SetObjectiveProgress(int objectiveIndex, int progress)
    {
        if (objectiveIndex < 0 || objectiveIndex >= objectives.Count) return;
        objectives[objectiveIndex].currentProgress = progress;
        currentStatus = CheckCompletionStatus();
    }

    private Status CheckCompletionStatus()
    {
        if (currentStatus == Status.Completed) return Status.Completed;
        bool allComplete = true;
        foreach (var obj in objectives)
        {
            if (obj.currentProgress < obj.targetValue) { allComplete = false; break; }
        }
        return allComplete ? Status.Completed : Status.InProgress;
    }

    // For serialization (extend GameState's JSON)
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    public void FromJson(string json)
    {
        JsonUtility.FromJsonOverwrite(json, this);
    }
}