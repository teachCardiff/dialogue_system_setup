using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Represents a player choice in a dialogue node.
/// Includes text, condition to show, consequence on select, and target node.
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    public string choiceText;
    public DialogueNode targetNode; // Branch to this node on select

    // Conditions to unlock this choice (AND logic if multiple)
    public List<Condition> conditions = new List<Condition>();

    // Consequence A(now SO-abased for reusability)
    public List<Consequence> consequences = new List<Consequence>();

    // NEW: Inline quest operations (author directly on the choice without creating separate SOs)
    [Header("Inline Quest Operations (Conditions)")]
    public List<QuestOperation> operationConditions = new List<QuestOperation>();

    [Header("Inline Quest Operations (Consequences)")]
    public List<QuestOperation> operationConsequences = new List<QuestOperation>();


    public bool IsAvailable(GameState gameState)
    {
        foreach (var cond in conditions)
        {
            if (!cond.IsMet(gameState)) return false;
        }

        // Also evaluate inline operation-based conditions (all must be true)
        foreach (var op in operationConditions)
        {
            if (op == null) continue;
            if (!op.Evaluate(gameState)) return false;
        }

        return true;
    }
}