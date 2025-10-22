using System;
using UnityEngine;

/// <summary>
/// Consequence to set or modify an int variable on GameState (e.g., +reputation).
/// Designer can choose between setting to an absolute value or modifying (adding) the current value.
/// Assign this SO to a DialogueChoice's consequences list.
/// </summary>
[CreateAssetMenu(fileName = "SetIntConsequenceSO", menuName = "Dialogue/Consequences/SetInt", order = 5)]
public class SetIntConsequenceSO : Consequence
{
    public enum Mode { Set, Modify }

    public string variableName;
    public Mode operation = Mode.Modify;

    [Tooltip("If operation is Set, this is the absolute value. If Modify, this is the delta to add (can be negative).")]
    public int value = 0;

    public override void Execute(GameState gameState)
    {
        if (gameState == null)
        {
            Debug.LogWarning("SetIntConsequenceSO.Execute called with null GameState");
            return;
        }
        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("SetIntConsequenceSO has no variableName set");
            return;
        }

        try
        {
            if (operation == Mode.Set)
            {
                gameState.SetInt(variableName, value);
                Debug.Log($"SetIntConsequence: {variableName} = {value}");
            }
            else
            {
                int current = 0;
                try { current = gameState.GetInt(variableName); } catch { /* defensive: if method missing or variable not present, assume 0 */ }
                int updated = current + value;
                gameState.SetInt(variableName, updated);
                Debug.Log($"ModifyIntConsequence: {variableName} changed {current} -> {updated} (delta {value})");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception executing SetIntConsequenceSO for '{variableName}': {ex.Message}");
        }
    }
}