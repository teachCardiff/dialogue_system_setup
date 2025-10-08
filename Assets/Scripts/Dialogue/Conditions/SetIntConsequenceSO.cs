using UnityEngine;

/// <summary>
/// Consequence to set an int variable on GameState (e.g., +reputation).
/// Assign this SO to a DialogueChoice's consequences list.
/// </summary>
[CreateAssetMenu(fileName = "SetIntConsequenceSO", menuName = "Dialogue/Consequences/SetInt", order = 5)]
public class SetIntConsequenceSO : Consequence
{
    public string variableName;
    public int valueToSet;

    public override void Execute(GameState gameState)
    {
        gameState.SetInt(variableName, valueToSet);
        Debug.Log($"Executed SetInt: {variableName} = {valueToSet}"); // Optional feedback
    }
}