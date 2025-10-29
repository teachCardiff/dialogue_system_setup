using System;
using UnityEngine;

[Serializable]
public struct VariableReference
{
    public enum Scope { Global, QuestScoped }

    public Scope scope;
    public string key; // variable key (e.g., "coins", "hasKey")
    public Quest quest; // used when Scope == QuestScoped

    // Resolve to a GameState key. For quest-scoped, prefixes with quest name when available.
    public string ResolveKey()
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (scope == Scope.Global) return key;
        var qn = quest != null ? quest.questName : null;
        return string.IsNullOrEmpty(qn) ? key : ($"{qn}.{key}");
    }

    public override string ToString()
    {
        switch (scope)
        {
            case Scope.Global:
                return string.IsNullOrEmpty(key) ? "<global:unset>" : key;
            case Scope.QuestScoped:
                var qn = quest != null ? quest.questName : "<quest?>";
                return string.IsNullOrEmpty(key) ? $"{qn}.<unset>" : $"{qn}.{key}";
            default:
                return key;
        }
    }
}
