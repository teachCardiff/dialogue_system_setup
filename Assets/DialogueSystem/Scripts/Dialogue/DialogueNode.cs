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

    // ===== Audio & Flow (per-node overrides) =====
    public enum VoiceWaitMode { None = 0, TextOnly = 1, AudioOnly = 2, Both = 3, Either = 4 }

    [System.Serializable]
    public class NodePlaybackSettings
    {
        [Header("Voice Line")]
        public AudioClip voiceClip;
        [Range(0f, 1f)] public float voiceVolume = 1f;

        [Header("Completion Rule")]
        [Tooltip("Use node-specific wait mode instead of the global setting.")]
        public bool overrideWaitMode = false;
        public VoiceWaitMode waitMode = VoiceWaitMode.Either;

        [Header("Auto-Advance Override")]
        [Tooltip("If enabled, this node overrides the global auto-advance setting.")]
        public bool overrideAutoAdvance = false;
        [Tooltip("When override is enabled, should this node auto-advance after completion?")]
        public bool autoAdvance = true;
        [Min(0f)] public float autoAdvanceDelay = 0.25f;

        [Header("Input Behavior")]
        [Tooltip("If true, player can skip/stop the currently playing voice with the advance input.")]
        public bool allowSkipAudio = true;
        [Tooltip("If true, voice playback stops when leaving this node.")]
        public bool stopVoiceOnExit = true;

        [Header("Pacing")]
        [Tooltip("If true and a voice clip exists, typewriter speed will be adjusted to roughly match the clip length.")]
        public bool matchTypewriterToAudio = false;
    }

    [Header("Audio & Flow (Overrides)")]
    public NodePlaybackSettings playback = new NodePlaybackSettings();
}