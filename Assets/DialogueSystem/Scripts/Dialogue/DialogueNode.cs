using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TextCore.Text;

/// <summary>
/// ScriptableObject for a single dialogue node.
/// Contains speaker, text, choices, and links to next nodes.
/// Choices have conditions and consequences.
/// </summary>
[CreateAssetMenu(fileName = "DialogueNode", menuName = "Dialogue/DialogueNode", order = 3)]
public class DialogueNode : ScriptableObject
{
    public string nodeId; // Unique ID for saving/linking
    public string speakerName;
    public Character speakerCharacter;
    public string speakerExpression;
    // Optional 2nd character to display both sprites
    public Character listenerCharacter;
    public string listenerExpression;
    public bool listenerIsSpeaker;
    [TextArea(3, 10)] public string dialogueText;

    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [System.Serializable]
    public class ConditionalBranch
    {
        public string branchName = "New Branch";
        public List<VariableOperation> operations = new List<VariableOperation>(); // AND logic
        public DialogueNode targetNode; // Branch to here if met
    }

    [Header("Conditional Branches (Auto-Branch on Enter)")]
    public List<ConditionalBranch> conditionalBranches = new List<ConditionalBranch>();

    // Events for extensibility (e.g., play animation on node enter)
    public UnityEvent onEnterNode;
    public UnityEvent onExitNode;
    // New: variable-driven actions to execute when this node exits
    public List<VariableAction> exitActions = new List<VariableAction>();

    // If no choices, next node is linear progression
    public DialogueNode nextNode;

    public bool IsEndNode => choices.Count == 0 && nextNode == null;
}