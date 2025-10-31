using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;

[CreateAssetMenu(fileName = "GameState", menuName = "Dialogue/GameState", order = 1)]
public class GameState : ScriptableObject
{
    // Events for notifying changes (e.g., UI updates)
    public UnityEvent onStateChanged;

    // ---------------- Variables V2 ----------------
    [Header("Variables V2")]
    [SerializeReference] public VariableGroup root; // single source of truth

    [Header("Characters")]
    public List<Character> characters = new List<Character>();

    private void OnEnable()
    {
        if (root == null)
            root = new VariableGroup { Key = "Root", DisplayName = "Root" };

        // Ensure default groups exist
        root.EnsureGroup("Quests");
        root.EnsureGroup("Player");
        root.EnsureGroup("Flags");

        // Rebuild transient parent pointers after deserialization
        root.RebuildParentLinks();

        // NEW: ensure every variable has a persistent GUID so VariableRef lookups work in play mode
        if (root.EnsureAllIdsAssigned())
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (root == null) return;
        root.RebuildParentLinks();
        if (root.EnsureAllIdsAssigned())
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    // Resolve by GUID
    public Variable TryResolveById(string id)
    {
        if (root == null || string.IsNullOrEmpty(id)) return null;
        return root.FindByGuid(id);
    }

    // Generic Get/Set by GUID
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
        if (v is VariableValue<T> tv) tv.value = value; else v.SetBoxed(value);
        onStateChanged?.Invoke();
        return true;
    }

    // Quest helpers (V2 only)
    public QuestVariable CreateQuest(string questKey, string displayName = null)
    {
        var quests = root?.EnsureGroup("Quests");
        if (quests == null) return null;
        var q = new QuestVariable { Key = questKey, DisplayName = string.IsNullOrEmpty(displayName) ? questKey : displayName };
        q.status.value = QuestStatus.NotStarted;
        quests.AddChild(q);
        // Ensure IDs on new content too
        root.RebuildParentLinks();
        root.EnsureAllIdsAssigned();
        onStateChanged?.Invoke();
        return q;
    }

    public bool IsQuestNotStarted(string questName) => !IsQuestStarted(questName);
    public bool IsQuestStarted(string questName)
    {
        var q = root?.FindByPath($"Quests/{questName}") as QuestVariable;
        return q != null && q.status.value != QuestStatus.NotStarted;
    }
    public bool IsQuestCompleted(string questName)
    {
        var q = root?.FindByPath($"Quests/{questName}") as QuestVariable;
        return q != null && q.status.value == QuestStatus.Completed;
    }

    // Evaluate AND list of operations
    public bool EvaluateOperations(List<VariableOperation> ops)
    {
        if (ops == null || ops.Count == 0) return true;
        foreach (var op in ops) if (!op.Evaluate(this)) return false;
        return true;
    }

    // Apply actions
    public void ApplyActions(List<VariableAction> actions)
    {
        if (actions == null) return;
        foreach (var a in actions) a.Apply(this);
        if (actions.Count > 0) onStateChanged?.Invoke();
    }

    // Reset V2 tree: non-destructive. Only reset quests to defaults and zero objective progress.
    public void ResetV2()
    {
        if (root == null)
        {
            root = new VariableGroup { Key = "Root", DisplayName = "Root" };
        }
        // Ensure groups exist but DO NOT replace the tree
        root.EnsureGroup("Quests");
        root.EnsureGroup("Player");
        root.EnsureGroup("Flags");
        root.RebuildParentLinks();

        int questCount = 0;
        int objectiveCount = 0;
        foreach (var v in VariableGroup.Traverse(root))
        {
            if (v is QuestVariable q)
            {
                q.status.value = QuestStatus.NotStarted;
                questCount++;
                if (q.objectives != null)
                {
                    for (int i = 0; i < q.objectives.Count; i++)
                    {
                        var obj = q.objectives[i];
                        if (obj != null)
                        {
                            obj.progress.value = 0;
                            objectiveCount++;
                        }
                    }
                }
            }
        }

        // Maintain existing IDs for all variables
        root.EnsureAllIdsAssigned();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"GameState ResetV2: reset {questCount} quest(s), {objectiveCount} objective(s) to defaults without removing other variables.");
        onStateChanged?.Invoke();
    }

    // Save/load V2
    public string ToJson()
    {
        var state = new SerializableStateV2 { root = this.root, characters = this.characters };
        return JsonUtility.ToJson(state, true);
    }

    public void FromJson(string json)
    {
        var temp = JsonUtility.FromJson<SerializableStateV2>(json);
        root = temp.root ?? new VariableGroup { Key = "Root", DisplayName = "Root" };
        characters = temp.characters ?? new List<Character>();
        root.RebuildParentLinks();
        root.EnsureAllIdsAssigned();
    }

    [Serializable]
    private class SerializableStateV2
    {
        [SerializeReference] public VariableGroup root;
        public List<Character> characters;
    }
}