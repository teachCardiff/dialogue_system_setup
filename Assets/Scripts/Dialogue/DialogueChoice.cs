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


    public bool IsAvailable(GameState gameState)
    {
        foreach (var cond in conditions)
        {
            if (!cond.IsMet(gameState)) return false;
        }
        return true;
    }
}