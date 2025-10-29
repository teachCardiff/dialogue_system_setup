using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// ScriptableObject to hold global game state variables in a hierarchical, polymorphic tree.
/// Variables (ints, bools, strings, quests, etc.) live under a single root group.
/// Dialogue conditions/consequences operate on VariableRefs (GUID-based).
/// Legacy flat lists are kept temporarily as shims.
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

    // ---------------- New Variable System ----------------
    [Header("Variables V2")]
    [SerializeReference] public VariableGroup root; // single source of truth for variables

    [Header("Characters")]
    public List<Character> characters = new List<Character>();

    private void OnEnable()
    {
        if (root == null)
        {
            root = new VariableGroup { Key = "Root", DisplayName = "Root" };
        }
        // Ensure default groups exist
        var quests = root.EnsureGroup("Quests");
        var player = root.EnsureGroup("Player");
        var flags = root.EnsureGroup("Flags");
        var legacy = root.EnsureGroup("Legacy");
        legacy.EnsureGroup("Ints");
        legacy.EnsureGroup("Bools");
        legacy.EnsureGroup("Strings");

        // Rebuild transient parent pointers after deserialization and clean nulls
        root.RebuildParentLinks();
    }

    // Resolve by GUID
    public Variable TryResolveById(string id)
    {
        if (root == null || string.IsNullOrEmpty(id)) return null;
        return root.FindByGuid(id);
    }

    // Convenience Get/Set
    public T Get<T>(string variableId, T defaultValue = default)
    {
        var v = TryResolveById(variableId);
        if (v is VariableValue<T> tv) return tv.value;
        var boxed = v?.GetBoxed();
        if (boxed is T t) return t;
        return defaultValue;
    }

    public bool Set<T>(string variableId, T value)
    {
        var v = TryResolveById(variableId);
        if (v == null) return false;
        if (v is VariableValue<T> tv)
        {
            tv.value = value;
        }
        else
        {
            v.SetBoxed(value);
        }
        onStateChanged?.Invoke();
        return true;
    }

    // Helper to create a quest under Quests group
    public QuestVariable CreateQuest(string questKey, string displayName = null)
    {
        var quests = root?.EnsureGroup("Quests");
        if (quests == null) return null;
        var q = new QuestVariable();
        q.Key = questKey;
        q.DisplayName = string.IsNullOrEmpty(displayName) ? questKey : displayName;
        // init defaults
        q.status.value = QuestStatus.NotStarted;
        quests.AddChild(q);
        onStateChanged?.Invoke();
        return q;
    }

    public QuestStatus GetQuestStatusById(string questVarId)
    {
        var v = TryResolveById(questVarId) as QuestVariable;
        return v != null ? v.status.value : QuestStatus.NotStarted;
    }

    public bool SetQuestStatusById(string questVarId, QuestStatus status)
    {
        var v = TryResolveById(questVarId) as QuestVariable;
        if (v == null) return false;
        v.status.value = status;
        onStateChanged?.Invoke();
        return true;
    }

    // Evaluate AND list of operations
    public bool EvaluateOperations(List<VariableOperation> ops)
    {
        if (ops == null || ops.Count == 0) return true;
        foreach (var op in ops)
        {
            if (!op.Evaluate(this)) return false;
        }
        return true;
    }

    public void ApplyActions(List<VariableAction> actions)
    {
        if (actions == null) return;
        foreach (var a in actions) a.Apply(this);
        if (actions.Count > 0) onStateChanged?.Invoke();
    }

    // ---------------- Legacy Shims ----------------
    public int GetInt(string name)
    {
        var @var = intVariables.Find(v => v.name == name);
        return @var != null ? @var.value : 0;
    }

    public void SetInt(string name, int value)
    {
        var @var = intVariables.Find(v => v.name == name);
        if (@var != null) @var.value = value;
        else intVariables.Add(new IntVariable { name = name, value = value });
        onStateChanged?.Invoke();
    }

    // Similar methods for bool and string...
    public bool GetBool(string name)
    {
        var @var = boolVariables.Find(v => v.name == name);
        return @var != null ? @var.value : false;
    }

    public void SetBool(string name, bool value)
    {
        var @var = boolVariables.Find(v => v.name == name);
        if (@var != null) @var.value = value;
        else boolVariables.Add(new BoolVariable { name = name, value = value });
        onStateChanged?.Invoke();
    }

    public string GetString(string name)
    {
        var @var = stringVariables.Find(v => v.name == name);
        return @var != null ? @var.value : string.Empty;
    }

    public void SetString(string name, string value)
    {
        var @var = stringVariables.Find(v => v.name == name);
        if (@var != null) @var.value = value;
        else stringVariables.Add(new StringVariable { name = name, value = value });
        onStateChanged?.Invoke();
    }

    [System.Serializable]
    public class QuestEntry { public string questName; public string questJson; }
    [System.Serializable]
    public class CompletedQuestEntry { public string questName; public Quest.Status finalStatus; }

    public List<QuestEntry> activeQuests = new List<QuestEntry>(); // Tracked quests (legacy)
    public List<CompletedQuestEntry> completedQuests = new List<CompletedQuestEntry>(); // Archive (legacy)

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
        // New flow: create quest variable in V2 if not present
        var quests = root?.EnsureGroup("Quests");
        var existing = quests?.FindByPath($"{qName}") as QuestVariable;
        if (existing == null)
        {
            existing = CreateQuest(qName, quest.description);
            // migrate objectives counts if needed
            foreach (var obj in quest.objectives)
            {
                var o = new ObjectiveVariable { Key = obj.description, DisplayName = obj.description };
                o.progress.value = obj.currentProgress;
                o.target.value = obj.targetValue;
                existing.objectives.Add(o);
            }
        }
        existing.status.value = QuestStatus.InProgress;
        onStateChanged?.Invoke();

        // Keep legacy list minimally in sync (optional)
        var tempQuest = ScriptableObject.CreateInstance<Quest>();
        JsonUtility.FromJsonOverwrite(quest.ToJson(), tempQuest);
        tempQuest.currentStatus = Quest.Status.InProgress;
        var entry = new QuestEntry { questName = qName, questJson = tempQuest.ToJson() };
        activeQuests.Add(entry);
        DestroyImmediate(tempQuest);
    }

    public void UpdateQuestProgress(string questName, int objectiveIndex, int progress)
    {
        // Update V2
        var q = root?.FindByPath($"Quests/{questName}") as QuestVariable;
        if (q != null && objectiveIndex >= 0 && objectiveIndex < q.objectives.Count)
        {
            q.objectives[objectiveIndex].progress.value = progress;
            if (q.objectives.TrueForAll(o => o.Completed)) q.status.value = QuestStatus.Completed;
            onStateChanged?.Invoke();
        }

        // Legacy fallback
        var legacyQuest = GetQuest(questName);
        if (legacyQuest != null)
        {
            legacyQuest.SetObjectiveProgress(objectiveIndex, progress);
            var entry = activeQuests.Find(qe => qe.questName == questName);
            if (entry != null) entry.questJson = legacyQuest.ToJson();
            if (legacyQuest.currentStatus == Quest.Status.Completed)
            {
                activeQuests.RemoveAll(a => a.questName == questName);
                completedQuests.Add(new CompletedQuestEntry { questName = questName, finalStatus = Quest.Status.Completed });
            }
            DestroyImmediate(legacyQuest);
        }
    }

    public bool IsQuestNotStarted(string questName)
    {
        return !IsQuestStarted(questName);
    }

    public bool IsQuestStarted(string questName)
    {
        // V2: started if status != NotStarted
        var q = root?.FindByPath($"Quests/{questName}") as QuestVariable;
        if (q != null) return q.status.value != QuestStatus.NotStarted;
        // Legacy fallback
        return activeQuests.Exists(qe => qe.questName == questName) || IsQuestCompleted(questName);
    }

    public bool IsQuestCompleted(string questName)
    {
        var q = root?.FindByPath($"Quests/{questName}") as QuestVariable;
        if (q != null) return q.status.value == QuestStatus.Completed;
        return completedQuests.Exists(qe => qe.questName == questName);
    }

    public void CompleteQuest(string questName)
    {
        var q = root?.FindByPath($"Quests/{questName}") as QuestVariable;
        if (q != null)
        {
            q.status.value = QuestStatus.Completed;
            onStateChanged?.Invoke();
        }
        // Legacy sync
        var quest = GetQuest(questName);
        if (quest != null)
        {
            quest.currentStatus = Quest.Status.Completed;
            var entry = activeQuests.Find(qe => qe.questName == questName);
            if (entry != null) entry.questJson = quest.ToJson();
            completedQuests.Add(new CompletedQuestEntry { questName = questName, finalStatus = Quest.Status.Completed });
            activeQuests.RemoveAll(a => a.questName == questName);
            DestroyImmediate(quest);
        }
    }

    // For save/load: Serialize to JSON
    public string ToJson()
    {
        var state = new SerializableStateV2
        {
            root = this.root,
            characters = this.characters
        };
        return JsonUtility.ToJson(state, true);
    }

    public void FromJson(string json)
    {
        var temp = JsonUtility.FromJson<SerializableStateV2>(json);
        root = temp.root ?? new VariableGroup { Key = "Root", DisplayName = "Root" };
        characters = temp.characters ?? new List<Character>();
        root.RebuildParentLinks();
    }

    [System.Serializable]
    private class SerializableStateV2
    {
        [SerializeReference] public VariableGroup root;
        public List<Character> characters;
    }
}