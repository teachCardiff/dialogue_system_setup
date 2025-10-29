using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Dialogue/Conditions/QuestOperationCondition")]
public class QuestOperationCondition : Condition
{
    public List<QuestOperation> operations = new List<QuestOperation>();

    public override bool IsMet(GameState gameState)
    {
        if (gameState == null) return false;

        // All operations must evaluate to true for the condition to be met
        foreach (var op in operations)
        {
            if (op == null) continue;
            if (!op.Evaluate(gameState)) return false;
        }
        return true;
    }
}
