// filepath: Assets/DialogueSystem/Scripts/Dialogue/Variables/Operations.cs
using System;
using UnityEngine;

[Serializable]
public class VariableRef
{
    public string id; // GUID to a Variable
    public override string ToString() => id;
    public bool IsValid => !string.IsNullOrEmpty(id);
}

public enum NumericOperator { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }
public enum BoolOperator { Equal, NotEqual, IsTrue, IsFalse }
public enum StringOperator { Equal, NotEqual, Contains, StartsWith, EndsWith }
public enum EnumOperator { Equal, NotEqual }

[Serializable]
public class VariableOperation
{
    public VariableRef variable = new VariableRef();

    // Type-specific operators/values
    public NumericOperator numericOp = NumericOperator.Equal;
    public int intValue;

    public BoolOperator boolOp = BoolOperator.Equal;
    public bool boolValue;

    public StringOperator stringOp = StringOperator.Equal;
    public string stringValue;

    public EnumOperator enumOp = EnumOperator.Equal;
    public string enumString; // serialized enum name for comparison

    public bool Evaluate(GameState gameState)
    {
        if (gameState == null || variable == null || string.IsNullOrEmpty(variable.id)) return false;
        var v = gameState.TryResolveById(variable.id);
        if (v == null) return false;

        // Int
        if (v is VariableValue<int> iv)
        {
            switch (numericOp)
            {
                case NumericOperator.Equal: return iv.value == intValue;
                case NumericOperator.NotEqual: return iv.value != intValue;
                case NumericOperator.Greater: return iv.value > intValue;
                case NumericOperator.GreaterOrEqual: return iv.value >= intValue;
                case NumericOperator.Less: return iv.value < intValue;
                case NumericOperator.LessOrEqual: return iv.value <= intValue;
            }
        }
        // Bool
        if (v is VariableValue<bool> bv)
        {
            switch (boolOp)
            {
                case BoolOperator.IsTrue: return bv.value == true;
                case BoolOperator.IsFalse: return bv.value == false;
                case BoolOperator.Equal: return bv.value == boolValue;
                case BoolOperator.NotEqual: return bv.value != boolValue;
            }
        }
        // String
        if (v is VariableValue<string> sv)
        {
            var a = sv.value ?? string.Empty;
            var b = stringValue ?? string.Empty;
            switch (stringOp)
            {
                case StringOperator.Equal: return string.Equals(a, b, StringComparison.Ordinal);
                case StringOperator.NotEqual: return !string.Equals(a, b, StringComparison.Ordinal);
                case StringOperator.Contains: return a.Contains(b, StringComparison.Ordinal);
                case StringOperator.StartsWith: return a.StartsWith(b, StringComparison.Ordinal);
                case StringOperator.EndsWith: return a.EndsWith(b, StringComparison.Ordinal);
            }
        }
        // Enum (QuestStatus etc.)
        if (v.ValueType != null && v.ValueType.IsEnum)
        {
            try
            {
                var parsed = Enum.Parse(v.ValueType, enumString, true);
                var current = v.GetBoxed();
                switch (enumOp)
                {
                    case EnumOperator.Equal: return Equals(current, parsed);
                    case EnumOperator.NotEqual: return !Equals(current, parsed);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Unsupported types return false by default
        return false;
    }
}

public enum ActionKind
{
    SetInt,
    IncInt,
    SetBool,
    ToggleBool,
    SetString,
    SetEnum, // by name
    SetQuestStatus,
    SetObjectiveProgress
}

[Serializable]
public class VariableAction
{
    public ActionKind kind = ActionKind.SetInt;
    public VariableRef variable = new VariableRef();

    // Common payloads
    public int intValue;
    public bool boolValue;
    public string stringValue;

    // For enums
    public string enumString;

    // For objective progress
    public int objectiveIndex;

    public void Apply(GameState gameState)
    {
        if (gameState == null || variable == null || string.IsNullOrEmpty(variable.id)) return;
        var v = gameState.TryResolveById(variable.id);
        if (v == null) return;

        switch (kind)
        {
            case ActionKind.SetInt:
                if (v is VariableValue<int> iv) iv.value = intValue;
                break;
            case ActionKind.IncInt:
                if (v is VariableValue<int> iv2) iv2.value += intValue;
                break;
            case ActionKind.SetBool:
                if (v is VariableValue<bool> bv) bv.value = boolValue;
                break;
            case ActionKind.ToggleBool:
                if (v is VariableValue<bool> bv2) bv2.value = !bv2.value;
                break;
            case ActionKind.SetString:
                if (v is VariableValue<string> sv) sv.value = stringValue;
                break;
            case ActionKind.SetEnum:
                if (v.ValueType != null && v.ValueType.IsEnum)
                {
                    try
                    {
                        var parsed = Enum.Parse(v.ValueType, enumString, true);
                        v.SetBoxed(parsed);
                    }
                    catch (Exception) { }
                }
                break;
            case ActionKind.SetQuestStatus:
                if (v is QuestVariable qv)
                {
                    if (Enum.TryParse<QuestStatus>(enumString, true, out var status))
                    {
                        qv.status.value = status;
                    }
                }
                break;
            case ActionKind.SetObjectiveProgress:
                if (v is QuestVariable qv2)
                {
                    if (objectiveIndex >= 0 && objectiveIndex < qv2.objectives.Count)
                    {
                        qv2.objectives[objectiveIndex].progress.value = intValue;
                        if (qv2.objectives.TrueForAll(o => o.Completed)) qv2.status.value = QuestStatus.Completed;
                    }
                }
                break;
        }
    }
}
