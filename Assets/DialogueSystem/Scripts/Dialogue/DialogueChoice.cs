using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a player choice in a dialogue node.
/// Includes text, criteria to show/enable, actions on select, and target node.
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    public string choiceText;
    public DialogueNode targetNode; // Branch to this node on select

    // Criteria to unlock this choice (AND logic)
    public List<VariableOperation> criteria = new List<VariableOperation>();

    // If true, show the choice even when criteria not met (but disabled). If false, hide it.
    public bool showIfCriteriaNotMet = false;

    // Actions applied when this choice is selected
    public List<VariableAction> consequences = new List<VariableAction>();

    public bool IsAvailable(GameState gameState)
    {
        return gameState != null && gameState.EvaluateOperations(criteria);
    }
}