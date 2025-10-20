using System;
using UnityEngine;

[CreateAssetMenu(fileName = "CheckBoolConditionSO", menuName = "Dialogue/Conditions/CheckBool", order = 6)]
public class CheckBoolConditionSO : Condition
{
    public string variableName;
    public bool requiredValue = true;

    public override bool IsMet(GameState gameState)
    {
        if (gameState == null)
        {
            Debug.LogWarning("CheckBoolConditionSO.IsMet called with null GameState");
            return false;
        }
        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("CheckBoolConditionSO has no variableName set");
            return false;
        }

        try
        {
            bool current = false;
            try { current = gameState.GetBool(variableName); } catch { /* assume false if missing */ }
            return current == requiredValue;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception evaluating CheckBoolConditionSO for '{variableName}': {ex.Message}");
            return false;
        }
    }
}
