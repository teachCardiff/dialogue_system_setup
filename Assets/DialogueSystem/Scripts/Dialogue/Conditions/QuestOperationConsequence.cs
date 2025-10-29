using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Consequences/QuestOperationConsequence")]
public class QuestOperationConsequence : Consequence
{
    public List<QuestOperation> operations = new List<QuestOperation>();

    public override void Execute(GameState gameState)
    {
        if (gameState == null)
        {
            Debug.LogWarning("QuestOperationConsequence.Execute called with null GameState");
            return;
        }

        foreach (var op in operations)
        {
            if (op == null) continue;
            try
            {
                op.Execute(gameState);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error executing quest operation: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
