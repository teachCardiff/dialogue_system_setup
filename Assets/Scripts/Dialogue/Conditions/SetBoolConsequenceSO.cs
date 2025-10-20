using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SetBoolConsequenceSO", menuName = "Dialogue/Consequences/SetBool", order = 6)]
public class SetBoolConsequenceSO : Consequence
{
    public string variableName;
    public bool value = true;

    public override void Execute(GameState gameState)
    {
        if (gameState == null)
        {
            Debug.LogWarning("SetBoolConsequenceSO.Execute called with null GameState");
            return;
        }
        if (string.IsNullOrEmpty(variableName))
        {
            Debug.LogWarning("SetBoolConsequenceSO has no variableName set");
            return;
        }

        try
        {
            gameState.SetBool(variableName, value);
            Debug.Log($"SetBoolConsequence: {variableName} = {value}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception executing SetBoolConsequenceSO for '{variableName}': {ex.Message}");
        }
    }
}
