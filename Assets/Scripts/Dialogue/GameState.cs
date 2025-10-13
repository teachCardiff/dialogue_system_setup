using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ScriptableObject to hold global game state variables (ints, bools, strings).
/// Conditions check these, and consequences update them.
/// Extensible for any project-specific needs.
/// </summary>
[CreateAssetMenu(fileName = "GameState", menuName = "Dialogue/GameState", order = 1)]
public class GameState : ScriptableObject
{
    [System.Serializable]
    public class IntVariable { public string name; public int value; }
    [System.Serializable]
    public class BoolVariable { public string name; public bool value; }
    [System.Serializable]
    public class StringVariable { public string name; public string value; }

    public List<IntVariable> intVariables = new List<IntVariable>();
    public List<BoolVariable> boolVariables = new List<BoolVariable>();
    public List<StringVariable> stringVariables = new List<StringVariable>();

    // UnityEvent for notifying changes (e.g., UI updates)
    public UnityEvent onStateChanged;

    public int GetInt(string name)
    {
        var var = intVariables.Find(v => v.name == name);
        return var != null ? var.value : 0;
    }

    public void SetInt(string name, int value)
    {
        var var = intVariables.Find(v => v.name == name);
        if (var != null) var.value = value;
        else intVariables.Add(new IntVariable { name = name, value = value });
        onStateChanged?.Invoke();
    }

    // Similar methods for bool and string...
    public bool GetBool(string name)
    {
        var var = boolVariables.Find(v => v.name == name);
        return var != null ? var.value : false;
    }

    public void SetBool(string name, bool value)
    {
        var var = boolVariables.Find(v => v.name == name);
        if (var != null) var.value = value;
        else boolVariables.Add(new BoolVariable { name = name, value = value });
        onStateChanged?.Invoke();
    }

    public string GetString(string name)
    {
        var var = stringVariables.Find(v => v.name == name);
        return var != null ? var.value : string.Empty;
    }

    public void SetString(string name, string value)
    {
        var var = stringVariables.Find(v => v.name == name);
        if (var != null) var.value = value;
        else stringVariables.Add(new StringVariable { name = name, value = value });
        onStateChanged?.Invoke();
    }

    [System.Serializable]
    public class QuestEntry { public string questName; public string questJson; }
    [System.Serializable]
    public class CompletedQuestEntry { public string questName; public Quest.Status finalStatus; }

    public List<QuestEntry> activeQuests = new List<QuestEntry>(); // Tracked quests
    public List<CompletedQuestEntry> completedQuests = new List<CompletedQuestEntry>(); // Archive completed quests

    public Quest GetQuest(string questName)
    {
        var entry = activeQuests.Find(q => q.questName == questName);
        if (entry == null) return null;
        
        var quest = ScriptableObject.CreateInstance<Quest>();
        quest.FromJson(entry.questJson);
        return quest;
    }

    public void StartQuest(Quest quest)
    {
        if (quest == null) return;

        string qName = quest.questName;
        if (GetQuest(qName) != null || IsQuestCompleted(qName)) return; // Avoid duplicates or restarting completed quests

        var tempQuest = ScriptableObject.CreateInstance<Quest>();
        JsonUtility.FromJsonOverwrite(quest.ToJson(), tempQuest);
        tempQuest.currentStatus = Quest.Status.InProgress;

        var entry = new QuestEntry { questName = qName, questJson = tempQuest.ToJson() };
        activeQuests.Add(entry);

        DestroyImmediate(tempQuest);
        onStateChanged?.Invoke();
    }

    public void UpdateQuestProgress(string questName, int objectiveIndex, int progress)
    {
        var quest = GetQuest(questName);
        if (quest != null)
        {
            quest.SetObjectiveProgress(objectiveIndex, progress);
            var entry = activeQuests.Find(q => q.questName == questName);
            entry.questJson = quest.ToJson();

            if (quest.currentStatus == Quest.Status.Completed)
            {
                var activeEntry = activeQuests.Find(q => q.questName == questName);
                if (activeEntry != null)
                {
                    completedQuests.Add(new CompletedQuestEntry { questName = questName, finalStatus = Quest.Status.Completed });
                    activeQuests.Remove(activeEntry);
                    Debug.Log($"Archived completed quest: {questName}");
                }
            }

            DestroyImmediate(quest);
            onStateChanged?.Invoke();
        }
    }

    public bool IsQuestNotStarted(string questName)
    {
        return !IsQuestStarted(questName);
    }

    public bool IsQuestStarted(string questName)
    {
        return activeQuests.Exists(q => q.questName == questName) || IsQuestCompleted(questName);
    }

    public bool IsQuestCompleted(string questName)
    {
        return completedQuests.Exists(q => q.questName == questName);
    }

    public void CompleteQuest(string questName)
    {
        var quest = GetQuest(questName);
        if (quest != null)
        {
            quest.currentStatus = Quest.Status.Completed;
            var entry = activeQuests.Find(q => q.questName == questName);
            entry.questJson = quest.ToJson(); // Optional, but ensures status is saved

            completedQuests.Add(new CompletedQuestEntry { questName = questName, finalStatus = Quest.Status.Completed });
            activeQuests.Remove(entry);

            DestroyImmediate(quest);
            onStateChanged?.Invoke();
        }
    }

    // For save/load: Serialize to JSON
    public string ToJson() // was public new string
    {
        var state = new { intVariables, boolVariables, stringVariables, activeQuests };
        return JsonUtility.ToJson(state, true);
    }

    public void FromJson(string json) // was public new void
    {
        // Parse the JSON manually or use a wrapper class for deserialization
        // For simplicity, assume we use a temp object; in production, use Newtonsoft.Json if needed
        var temp = JsonUtility.FromJson<SerializableGameState>(json);
        intVariables = temp.intVariables ?? new List<IntVariable>();
        boolVariables = temp.boolVariables ?? new List<BoolVariable>();
        stringVariables = temp.stringVariables ?? new List<StringVariable>();
        activeQuests = temp.activeQuests ?? new List<QuestEntry>();
    }

    [System.Serializable]
    private class SerializableGameState
    {
        public List<IntVariable> intVariables;
        public List<BoolVariable> boolVariables;
        public List<StringVariable> stringVariables;
        public List<QuestEntry> activeQuests;
    }
}