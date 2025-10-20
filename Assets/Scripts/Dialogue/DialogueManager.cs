using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton manager for running dialogues at runtime.
/// Handles progression, UI updates, and state changes.
/// Easy API: StartDialogue(dialogue);
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private GameState gameState;
    [SerializeField] private DialogueUI dialogueUI; // Reference to UI prefab instance

    private Dialogue currentDialogue;
    private DialogueNode currentNode;

    // Events for external integration (e.g., pause game on dialogue start)
    public UnityEvent onDialogueStart;
    public UnityEvent onDialogueEnd;

    // For debugging in Inspector
    [Header("Debug")]
    public DialogueNode debugCurrentNode;
    public List<DialogueChoice> debugAvailableChoices;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            if (dialogueUI != null && dialogueUI.gameObject.scene.name == null)
            {
                dialogueUI = Instantiate(dialogueUI, transform);
                dialogueUI.name = "DialogueUI_Instance";
            }

            if (dialogueUI == null)
            {
                Debug.LogError("DialogueUI prefab not assigned! Create and assign 'DialogeUI.prefab'.");
            }
        }
        else Destroy(gameObject);
    }

    public void StartDialogue(Dialogue dialogue, bool resetProgress = true)
    {
        if (currentDialogue != null) return; // Prevent overlap
        print("Starting the Dialogue");

        if (resetProgress)
        {
            dialogue.ResetProgress();
        }

        currentDialogue = dialogue;
        currentNode = dialogue.currentNode ?? dialogue.startNode;

        if (currentNode == null)
        {
            Debug.LogError($"Cannot start dialogue '{dialogue.name}': No startNode set! Set one in the Dialogue Editor.");
            EndDialogue(); // Clean exit
            return;
        }

        onDialogueStart.Invoke();
        StartCoroutine(RunNode());
    }

    private IEnumerator RunNode()
    {
        if (currentNode != null)
            Debug.Log($"[DialogueManager] Entering node: {currentNode.name} (id={currentNode.nodeId})");
        else
            Debug.Log("[DialogueManager] RunNode called but currentNode == null");
        
        debugCurrentNode = currentNode;
        currentNode.onEnterNode?.Invoke();
        // Auto-branch if conditions met (before showing this node)
        if (currentNode.conditionalBranches.Count > 0)
        {
            foreach (var branch in currentNode.conditionalBranches)
            {
                if (branch.condition != null && branch.condition.IsMet(gameState))
                {
                    currentNode = branch.targetNode;
                    // Recurse immediately (skip showing this hub node)
                    yield return RunNode();
                    yield break; // Exit this iteration
                }
            }
        }

        if (string.IsNullOrWhiteSpace(currentNode.speakerName) && string.IsNullOrWhiteSpace(currentNode.dialogueText))
        {
            // If there are no conditional branches, just go to next node
            if (currentNode.nextNode != null)
            {
                currentNode = currentNode.nextNode;
                yield return RunNode();
                yield break;
            }
            // Or end dialogue if nothing else
            else if (currentNode.IsEndNode)
            {
                // Ensure exit callbacks and consequences run before ending
                ExitNodeAndApplyConsequences(currentNode);
                EndDialogue();
                yield break;
            }
        }

        Sprite speakerSprite = null;
        if (currentNode.speakerCharacter != null)
            speakerSprite = currentNode.speakerCharacter.GetSprite(currentNode.speakerExpression);

        Sprite listenerSprite = null;
        if (currentNode.listenerCharacter != null)
        {
            listenerSprite = currentNode.listenerCharacter.GetSprite(currentNode.listenerExpression);
        }

        // Update speaker name based on who is active speaker
        string speakerName = currentNode.speakerName;
        if (currentNode.listenerIsSpeaker && currentNode.listenerCharacter != null)
        {
            speakerName = currentNode.listenerCharacter.npcName;
        }
        dialogueUI.ShowDialogue(speakerName, currentNode.dialogueText, speakerSprite, listenerSprite);

        // dialogueUI.ShowDialogue(currentNode.speakerName, currentNode.dialogueText, speakerSprite, listenerSprite);

        // If the UI was inactive it may have deferred initialization for one frame. Wait until the UI reports ready.
        yield return new WaitUntil(() => dialogueUI != null && dialogueUI.IsReady);

        // Wait for typewriter to finish or for user to skip. Use the UI's visibility state instead of comparing strings
        while (!dialogueUI.IsTextFullyRevealed())
        {
            if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) // TODO: Update to integrate the input action asset
            {
                dialogueUI.SkipTypewriter();
            }
            yield return null;
        }

        if (currentNode.IsEndNode)
        {
            yield return new WaitUntil(() => dialogueUI.IsNextPressed()); // Wait for player to advance
            // Ensure exit callbacks and consequences run before ending
            ExitNodeAndApplyConsequences(currentNode);
            EndDialogue();
            yield break;
        }

        if (currentNode.choices.Count > 0)
        {
            var availableChoices = currentNode.choices.FindAll(c => c.IsAvailable(gameState));
            debugAvailableChoices = availableChoices;
            dialogueUI.ShowChoices(availableChoices);

            yield return new WaitUntil(() => dialogueUI.selectedChoice != null);
            var selected = dialogueUI.selectedChoice;

            // Invoke SO consequences
            foreach (var consequence in selected.consequences)
            {
                consequence.Execute(gameState);
            }

            var previousNode = currentNode;
            currentNode = selected.targetNode;
            dialogueUI.ClearChoices();
            // Invoke exit logic for the node we just left
            ExitNodeAndApplyConsequences(previousNode);
        }
        else
        {
            yield return new WaitUntil(() => dialogueUI.IsNextPressed());
            var previousNode = currentNode;
            currentNode = currentNode.nextNode;
            // Invoke exit logic for the node we just left
            ExitNodeAndApplyConsequences(previousNode);
        }
        // Update progress and continue
        if (currentNode != null)
        {
            currentDialogue.currentNode = currentNode;
            currentDialogue.visitedNodes[currentNode.nodeId] = true;
            yield return RunNode(); // Recurse
        }
        else
        {
            // Reached a null node; end dialogue gracefully
            EndDialogue();
            yield break;
        }
    }

    // Helper: invoke onExit and run exitConsequences safely
    private void ExitNodeAndApplyConsequences(DialogueNode node)
    {
        if (node == null) return;

        try
        {
            node.onExitNode?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Exception while invoking onExitNode for {node.name}: {ex.Message}");
        }

        if (node.exitConsequences != null)
        {
            foreach (var conseq in node.exitConsequences)
            {
                if (conseq == null) continue;
                try
                {
                    conseq.Execute(gameState);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Exception executing consequence on node {node.name}: {ex.Message}");
                }
            }
        }
    }

    private void EndDialogue()
    {
        Debug.Log("[DialogueManager] EndDialogue called. currentDialogue=" + (currentDialogue ? currentDialogue.name : "null") +
              " currentNode=" + (currentNode ? currentNode.name : "null"));
        print("End of dialogue");
        //dialogueUI.nextPressed = false; // Moved to DialogueUI.Hide()
        if (dialogueUI != null)
        {
            // Subscribe to the UI hide complete event, then trigger hide.
            dialogueUI.onHideComplete.AddListener(HandleOnHideComplete);
            dialogueUI.Hide();
        }
        else
        {
            // Fallback: no UI available, clean up immediately
            CleanupAfterDialogue();
        }
    }

    private void HandleOnHideComplete()
    {
        if (dialogueUI != null)
        {
            dialogueUI.onHideComplete.RemoveListener(HandleOnHideComplete);
        }

        CleanupAfterDialogue();
    }

    private void CleanupAfterDialogue()
    {
        onDialogueEnd.Invoke();
        currentDialogue = null;
        currentNode = null;
        debugCurrentNode = null;
        debugAvailableChoices.Clear();
    }

    // Save/load API
    public string SaveGameState() => gameState.ToJson();
    public void LoadGameState(string json) => gameState.FromJson(json);

    public string SaveDialogueProgress(Dialogue dialogue) => dialogue.ToJson();
    public void LoadDialogueProgress(Dialogue dialogue, string json) => dialogue.FromJson(json);
}