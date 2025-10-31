// filepath: Assets/DialogueSystem/Scripts/Dialogue/Variables/Operations.cs
using System;
using System.Linq;
using UnityEngine;

[Serializable]
public class VariableRef
{
    public string id; // GUID to a Variable
    // New: backup path (root-relative) used if id fails or mismatches type
    public string path;
    public override string ToString() => string.IsNullOrEmpty(id) ? path : id;
    public bool IsValid => !string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(path);
}

public enum NumericOperator { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }
public enum BoolOperator { Equal, NotEqual, IsTrue, IsFalse }
public enum StringOperator { Equal, NotEqual, Contains, StartsWith, EndsWith }
public enum EnumOperator { Equal, NotEqual }

internal static class EnumUtil
{
    public static bool TryParseFlexible(Type enumType, string raw, out object value)
    {
        value = null;
        if (enumType == null || !enumType.IsEnum) return false;
        if (string.IsNullOrEmpty(raw)) return false;

        try
        {
            value = Enum.Parse(enumType, raw, true);
            return true;
        }
        catch { }

        string norm(string s) => new string((s ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-').ToArray()).ToLowerInvariant();
        var target = norm(raw);
        foreach (var name in Enum.GetNames(enumType))
        {
            if (norm(name) == target)
            {
                try { value = Enum.Parse(enumType, name, true); return true; } catch { }
            }
        }
        return false;
    }
}

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

    private static Variable ResolveWithFallback(GameState gameState, VariableRef vr)
    {
        if (gameState == null || vr == null) return null;
        Variable v = null;
        if (!string.IsNullOrEmpty(vr.id)) v = gameState.TryResolveById(vr.id);
        if (v == null && !string.IsNullOrEmpty(vr.path)) v = gameState.root?.FindByPath(vr.path);
        return v;
    }

    public bool Evaluate(GameState gameState)
    {
        if (gameState == null || variable == null || (!variable.IsValid))
        {
            Debug.LogWarning($"[VariableOperation] Evaluate: invalid setup (missing id/path).");
            return false;
        }
        var v = ResolveWithFallback(gameState, variable);
        if (v == null)
        {
            Debug.LogWarning($"[VariableOperation] Evaluate: Could not resolve variable id='{variable.id}' path='{variable.path}'.");
            return false;
        }

        // Int
        if (v is VariableValue<int> iv)
        {
            bool result = false;
            switch (numericOp)
            {
                case NumericOperator.Equal: result = iv.value == intValue; break;
                case NumericOperator.NotEqual: result = iv.value != intValue; break;
                case NumericOperator.Greater: result = iv.value > intValue; break;
                case NumericOperator.GreaterOrEqual: result = iv.value >= intValue; break;
                case NumericOperator.Less: result = iv.value < intValue; break;
                case NumericOperator.LessOrEqual: result = iv.value <= intValue; break;
            }
            Debug.Log($"[VariableOperation] Int '{v.GetPath()}' op={numericOp} against {intValue} -> current={iv.value} result={result}");
            return result;
        }
        // Bool
        if (v is VariableValue<bool> bv)
        {
            bool result = false;
            switch (boolOp)
            {
                case BoolOperator.IsTrue: result = bv.value == true; break;
                case BoolOperator.IsFalse: result = bv.value == false; break;
                case BoolOperator.Equal: result = bv.value == boolValue; break;
                case BoolOperator.NotEqual: result = bv.value != boolValue; break;
            }
            Debug.Log($"[VariableOperation] Bool '{v.GetPath()}' op={boolOp} against {boolValue} -> current={bv.value} result={result}");
            return result;
        }
        // String
        if (v is VariableValue<string> sv)
        {
            var a = sv.value ?? string.Empty;
            var b = stringValue ?? string.Empty;
            bool result = false;
            switch (stringOp)
            {
                case StringOperator.Equal: result = string.Equals(a, b, StringComparison.Ordinal); break;
                case StringOperator.NotEqual: result = !string.Equals(a, b, StringComparison.Ordinal); break;
                case StringOperator.Contains: result = a.Contains(b, StringComparison.Ordinal); break;
                case StringOperator.StartsWith: result = a.StartsWith(b, StringComparison.Ordinal); break;
                case StringOperator.EndsWith: result = a.EndsWith(b, StringComparison.Ordinal); break;
            }
            Debug.Log($"[VariableOperation] String '{v.GetPath()}' op={stringOp} against '{b}' -> current='{a}' result={result}");
            return result;
        }
        // Enum (QuestStatus etc.)
        if (v.ValueType != null && v.ValueType.IsEnum)
        {
            // Fallback: if no enum value was specified in authoring, default to the first enum name
            string enumToParse = enumString;
            if (string.IsNullOrEmpty(enumToParse))
            {
                var names0 = System.Enum.GetNames(v.ValueType);
                if (names0 != null && names0.Length > 0)
                {
                    enumToParse = names0[0];
                    Debug.LogWarning($"[VariableOperation] Enum compare value missing for type '{v.ValueType?.Name}' on '{v.GetPath()}'. Defaulting to '{enumToParse}'.");
                }
            }

            if (EnumUtil.TryParseFlexible(v.ValueType, enumToParse, out var parsed))
            {
                var current = v.GetBoxed();
                bool result = false;
                switch (enumOp)
                {
                    case EnumOperator.Equal: result = Equals(current, parsed); break;
                    case EnumOperator.NotEqual: result = !Equals(current, parsed); break;
                }
                Debug.Log($"[VariableOperation] Enum '{v.GetPath()}' op={enumOp} against '{enumToParse}' -> current='{current}' result={result}");
                return result;
            }
            Debug.LogWarning($"[VariableOperation] Enum parse failed for type '{v.ValueType?.Name}' from '{enumString}'.");
            return false;
        }

        Debug.LogWarning($"[VariableOperation] Unsupported variable type for '{v.GetPath()}' (type='{v.ValueType?.Name ?? v.GetType().Name}')");
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
    SetObjectiveProgress,
    // New: modify objective progress arithmetically
    ModifyObjectiveProgress
}

// New: arithmetic operator for modifying objective progress
public enum ArithmeticOp { Add, Subtract, Multiply, Divide }

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

    // New: arithmetic operator + clamp flag for ModifyObjectiveProgress
    public ArithmeticOp arithmeticOp = ArithmeticOp.Add;
    public bool clampToTargetRange = true;

    private static Variable ResolveWithFallback(GameState gameState, VariableRef vr)
    {
        if (gameState == null || vr == null) return null;
        Variable v = null;
        if (!string.IsNullOrEmpty(vr.id)) v = gameState.TryResolveById(vr.id);
        if (v == null && !string.IsNullOrEmpty(vr.path)) v = gameState.root?.FindByPath(vr.path);
        return v;
    }

    public void Apply(GameState gameState)
    {
        if (gameState == null || variable == null || (!variable.IsValid))
        {
            Debug.LogWarning($"[VariableAction] {kind}: invalid setup (missing id/path). id='{variable?.id}' path='{variable?.path}'");
            return;
        }
        var v = ResolveWithFallback(gameState, variable);
        if (v == null)
        {
            Debug.LogWarning($"[VariableAction] {kind}: variable not found. id='{variable?.id}' path='{variable?.path}'");
            return;
        }

        switch (kind)
        {
            case ActionKind.SetInt:
                if (v is VariableValue<int> iv) { Debug.Log($"[VariableAction] SetInt '{v.GetPath()}' = {intValue} (was {iv.value})"); iv.value = intValue; }
                else Debug.LogWarning($"[VariableAction] SetInt: Target '{v.GetPath()}' is not an int.");
                break;
            case ActionKind.IncInt:
                if (v is VariableValue<int> iv2) { Debug.Log($"[VariableAction] IncInt '{v.GetPath()}' += {intValue} (was {iv2.value})"); iv2.value += intValue; }
                else Debug.LogWarning($"[VariableAction] IncInt: Target '{v.GetPath()}' is not an int.");
                break;
            case ActionKind.SetBool:
                if (v is VariableValue<bool> bv) { Debug.Log($"[VariableAction] SetBool '{v.GetPath()}' = {boolValue} (was {bv.value})"); bv.value = boolValue; }
                else Debug.LogWarning($"[VariableAction] SetBool: Target '{v.GetPath()}' is not a bool.");
                break;
            case ActionKind.ToggleBool:
                if (v is VariableValue<bool> bv2) { Debug.Log($"[VariableAction] ToggleBool '{v.GetPath()}' -> {(!bv2.value)} (was {bv2.value})"); bv2.value = !bv2.value; }
                else Debug.LogWarning($"[VariableAction] ToggleBool: Target '{v.GetPath()}' is not a bool.");
                break;
            case ActionKind.SetString:
                if (v is VariableValue<string> sv) { Debug.Log($"[VariableAction] SetString '{v.GetPath()}' = '{stringValue}' (was '{sv.value}')"); sv.value = stringValue; }
                else Debug.LogWarning($"[VariableAction] SetString: Target '{v.GetPath()}' is not a string.");
                break;
            case ActionKind.SetEnum:
                if (v.ValueType != null && v.ValueType.IsEnum)
                {
                    if (EnumUtil.TryParseFlexible(v.ValueType, enumString, out var parsed))
                    {
                        Debug.Log($"[VariableAction] SetEnum '{v.GetPath()}' = '{parsed}' from '{enumString}' (was '{v.GetBoxed()}')");
                        v.SetBoxed(parsed);
                    }
                    else
                    {
                        Debug.LogWarning($"[VariableAction] SetEnum: Could not parse '{enumString}' for enum type '{v.ValueType?.Name}'.");
                    }
                }
                else Debug.LogWarning($"[VariableAction] SetEnum: Target '{v.GetPath()}' is not an enum.");
                break;
            case ActionKind.SetQuestStatus:
            {
                // Accept either the quest container or a QuestStatus value leaf
                if (v is QuestVariable qv)
                {
                    if (EnumUtil.TryParseFlexible(typeof(QuestStatus), enumString, out var parsedQS))
                    {
                        Debug.Log($"[VariableAction] SetQuestStatus '{qv.GetPath()}/status' = {parsedQS} (was {qv.status.value})");
                        qv.status.value = (QuestStatus)parsedQS;
                    }
                    else Debug.LogWarning($"[VariableAction] SetQuestStatus: Could not parse '{enumString}' as QuestStatus.");
                }
                else if (v is VariableValue<QuestStatus> svq)
                {
                    if (EnumUtil.TryParseFlexible(typeof(QuestStatus), enumString, out var parsedQS2))
                    {
                        Debug.Log($"[VariableAction] SetQuestStatus leaf '{v.GetPath()}' = {parsedQS2} (was {svq.value})");
                        svq.value = (QuestStatus)parsedQS2;
                    }
                    else Debug.LogWarning($"[VariableAction] SetQuestStatus: Could not parse '{enumString}' as QuestStatus.");
                }
                else
                {
                    // Try fallback by path if id pointed to a wrong type due to stale mapping
                    if (!string.IsNullOrEmpty(variable.path))
                    {
                        var vf = gameState.root?.FindByPath(variable.path);
                        if (vf is QuestVariable qvf)
                        {
                            if (EnumUtil.TryParseFlexible(typeof(QuestStatus), enumString, out var parsedQS3))
                            {
                                Debug.Log($"[VariableAction] SetQuestStatus (fallback) '{qvf.GetPath()}/status' = {parsedQS3} (was {qvf.status.value})");
                                qvf.status.value = (QuestStatus)parsedQS3;
                                break;
                            }
                        }
                    }
                    Debug.LogWarning($"[VariableAction] SetQuestStatus: Target '{v.GetPath()}' is not a QuestVariable or QuestStatus value. id='{variable.id}' path='{variable.path}'");
                }
                break;
            }
            case ActionKind.SetObjectiveProgress:
                if (v is QuestVariable qv2)
                {
                    if (objectiveIndex >= 0 && objectiveIndex < qv2.objectives.Count)
                    {
                        var obj = qv2.objectives[objectiveIndex];
                        Debug.Log($"[VariableAction] SetObjectiveProgress '{qv2.GetPath()}/Objectives/{obj.Display}' progress = {intValue} (was {obj.progress.value})");
                        qv2.objectives[objectiveIndex].progress.value = intValue;
                        if (qv2.objectives.TrueForAll(o => o.Completed)) qv2.status.value = QuestStatus.ReadyToTurnIn;
                    }
                    else
                    {
                        Debug.LogWarning($"[VariableAction] SetObjectiveProgress: objective index {objectiveIndex} out of range for '{qv2.GetPath()}'.");
                    }
                }
                else Debug.LogWarning($"[VariableAction] SetObjectiveProgress: Target '{v.GetPath()}' is not a QuestVariable.");
                break;

            case ActionKind.ModifyObjectiveProgress:
                if (v is QuestVariable qv3)
                {
                    if (objectiveIndex >= 0 && objectiveIndex < qv3.objectives.Count)
                    {
                        var obj = qv3.objectives[objectiveIndex];
                        int before = obj.progress.value;
                        int operand = intValue;
                        int after = before;
                        switch (arithmeticOp)
                        {
                            case ArithmeticOp.Add: after = before + operand; break;
                            case ArithmeticOp.Subtract: after = before - operand; break;
                            case ArithmeticOp.Multiply: after = before * operand; break;
                            case ArithmeticOp.Divide:
                                if (operand == 0)
                                {
                                    Debug.LogWarning("[VariableAction] ModifyObjectiveProgress: divide by zero – ignoring.");
                                    return;
                                }
                                after = before / operand; break;
                        }
                        if (clampToTargetRange)
                        {
                            int max = Mathf.Max(0, obj.target.value);
                            after = Mathf.Clamp(after, 0, max);
                        }
                        Debug.Log($"[VariableAction] ModifyObjectiveProgress '{qv3.GetPath()}/Objectives/{obj.Display}' {arithmeticOp} {operand} -> {after} (was {before})");
                        obj.progress.value = after;
                        if (qv3.objectives.TrueForAll(o => o.Completed)) qv3.status.value = QuestStatus.ReadyToTurnIn;
                    }
                    else
                    {
                        Debug.LogWarning($"[VariableAction] ModifyObjectiveProgress: objective index {objectiveIndex} out of range for '{qv3.GetPath()}'.");
                    }
                }
                else Debug.LogWarning($"[VariableAction] ModifyObjectiveProgress: Target '{v.GetPath()}' is not a QuestVariable.");
                break;
        }
    }
}
