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
    private DialogueInteract pendingInteractor;

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

            // Ensure debug lists are valid so CleanupAfterDialogue can't throw
            if (debugAvailableChoices == null)
                debugAvailableChoices = new List<DialogueChoice>();
        }
        else Destroy(gameObject);
    }

    public void StartDialogue(Dialogue dialogue, bool resetProgress = true)
    {
        if (currentDialogue != null) return; // Prevent overlap
        Debug.Log($"[DialogueManager] StartDialogue('{dialogue?.name}') resetProgress={resetProgress}");

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

    /// <summary>
    /// Called by an interactable just before requesting StartDialogue so the manager
    /// can mark that interactable as consumed only if the dialogue actually displays content.
    /// </summary>
    public void RegisterPendingInteractor(DialogueInteract interactor)
    {
        pendingInteractor = interactor;
    }

    private IEnumerator RunNode()
    {
        debugCurrentNode = currentNode;
        Debug.Log($"[DialogueManager] Enter Node: '{currentNode.name}' (speaker='{currentNode.speakerName}', textLen={currentNode.dialogueText?.Length ?? 0})");
        currentNode.onEnterNode?.Invoke();
        
        // Auto-branch if conditions met (before showing this node)
        if (currentNode.conditionalBranches.Count > 0)
        {
            bool branchTaken = false;
            for (int i = 0; i < currentNode.conditionalBranches.Count; i++)
            {
                var branch = currentNode.conditionalBranches[i];
                bool hasOps = branch.operations != null && branch.operations.Count > 0;
                if (!hasOps)
                {
                    Debug.LogWarning($"[DialogueManager] Branch[{i}] '{branch.branchName}' has no operations and will be ignored to avoid always-true.");
                    continue;
                }
                bool criteria = gameState.EvaluateOperations(branch.operations);
                Debug.Log($"[DialogueManager] Branch[{i}] '{branch.branchName}': criteria={criteria} -> target='{branch.targetNode?.name}'");
                if (criteria)
                {
                    branchTaken = true;
                    currentNode = branch.targetNode;
                    Debug.Log($"[DialogueManager] Taking branch -> '{currentNode?.name}'");
                    yield return RunNode();
                    yield break; // Exit this iteration
                }
            }

            if (!branchTaken)
            {
                if (currentNode.nextNode != null)
                {
                    Debug.Log($"[DialogueManager] No conditional branches met for node '{currentNode.name}'. Advancing to nextNode '{currentNode.nextNode.name}'.");
                    currentNode = currentNode.nextNode;
                    yield return RunNode();
                    yield break;
                }
                else
                {
                    Debug.Log($"[DialogueManager] No conditional branches met for node '{currentNode.name}' and no nextNode. Ending dialogue.");
                    if (pendingInteractor != null && pendingInteractor.dialogueAsset == currentDialogue)
                    {
                        pendingInteractor.ShowLockedFeedback();
                        pendingInteractor = null;
                    }
                    EndDialogue();
                    yield break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(currentNode.speakerName) && string.IsNullOrWhiteSpace(currentNode.dialogueText))
        {
            if (currentNode.nextNode != null)
            {
                Debug.Log($"[DialogueManager] Empty node '{currentNode.name}', skipping to nextNode '{currentNode.nextNode.name}'.");
                currentNode = currentNode.nextNode;
                yield return RunNode();
                yield break;
            }
            else if (currentNode.IsEndNode)
            {
                Debug.Log($"[DialogueManager] End node reached: '{currentNode.name}'. Ending dialogue.");
                ExitNodeAndApplyActions(currentNode);
                if (pendingInteractor != null && pendingInteractor.dialogueAsset == currentDialogue)
                {
                    pendingInteractor.ShowLockedFeedback();
                    pendingInteractor = null;
                }
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

        if (pendingInteractor != null && pendingInteractor.dialogueAsset == currentDialogue)
        {
            pendingInteractor.Consume();
            pendingInteractor = null;
        }

        Debug.Log($"[DialogueManager] Show UI: speaker='{speakerName}', text='{currentNode.dialogueText}'");
        dialogueUI.ShowDialogue(speakerName, currentNode.dialogueText, speakerSprite, listenerSprite);

        // Wait until UI is ready
        yield return new WaitUntil(() => dialogueUI != null && dialogueUI.IsReady);

        // Wait for typewriter to finish or user skip
        while (!dialogueUI.IsTextFullyRevealed())
        {
            if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))
            {
                Debug.Log("[DialogueManager] Skip typewriter requested by input.");
                dialogueUI.SkipTypewriter();
            }
            yield return null;
        }

        if (currentNode.IsEndNode)
        {
            Debug.Log($"[DialogueManager] Node '{currentNode.name}' is end node. Waiting for Next press.");
            yield return new WaitUntil(() => dialogueUI.IsNextPressed());
            ExitNodeAndApplyActions(currentNode);
            if (pendingInteractor != null && pendingInteractor.dialogueAsset == currentDialogue)
            {
                pendingInteractor.ShowLockedFeedback();
                pendingInteractor = null;
            }
            EndDialogue();
            yield break;
        }

        if (currentNode.choices.Count > 0)
        {
            var visibleChoices = new List<DialogueChoice>();
            var enabledFlags = new List<bool>();
            for (int i = 0; i < currentNode.choices.Count; i++)
            {
                var c = currentNode.choices[i];
                bool available = c.IsAvailable(gameState);
                Debug.Log($"[DialogueManager] Choice[{i}] '{c.choiceText}' available={available} showIfNotMet={c.showIfCriteriaNotMet} target='{c.targetNode?.name}'");
                if (available)
                {
                    visibleChoices.Add(c);
                    enabledFlags.Add(true);
                }
                else if (c.showIfCriteriaNotMet)
                {
                    visibleChoices.Add(c);
                    enabledFlags.Add(false);
                }
            }

            debugAvailableChoices = visibleChoices;
            bool allowContinue = visibleChoices.Count == 0;
            Debug.Log($"[DialogueManager] Showing choices: count={visibleChoices.Count}, allowContinue={allowContinue}");

            dialogueUI.ShowChoices(visibleChoices, enabledFlags, allowContinue);

            if (allowContinue)
            {
                Debug.Log("[DialogueManager] No visible choices. Waiting for Next to continue to nextNode.");
                yield return new WaitUntil(() => dialogueUI.IsNextPressed());
                var previousNode = currentNode;
                currentNode = currentNode.nextNode;
                Debug.Log($"[DialogueManager] Continue pressed. Advancing to '{currentNode?.name}'. Applying exit actions for '{previousNode?.name}'.");
                dialogueUI.ClearChoices();
                ExitNodeAndApplyActions(previousNode);
            }
            else
            {
                Debug.Log("[DialogueManager] Waiting for a choice selection.");
                yield return new WaitUntil(() => dialogueUI.selectedChoice != null);
                var selected = dialogueUI.selectedChoice;
                Debug.Log($"[DialogueManager] Selected choice '{selected.choiceText}'. Applying {selected.consequences?.Count ?? 0} action(s).");
                gameState.ApplyActions(selected.consequences);
                var previousNode = currentNode;
                currentNode = selected.targetNode;
                dialogueUI.ClearChoices();
                ExitNodeAndApplyActions(previousNode);
            }
        }
        else
        {
            Debug.Log($"[DialogueManager] No choices at node '{currentNode.name}'. Waiting for Next to go to nextNode '{currentNode.nextNode?.name}'.");
            yield return new WaitUntil(() => dialogueUI.IsNextPressed());
            var previousNode = currentNode;
            currentNode = currentNode.nextNode;
            ExitNodeAndApplyActions(previousNode);
        }

        if (currentNode != null)
        {
            currentDialogue.currentNode = currentNode;
            currentDialogue.visitedNodes[currentNode.nodeId] = true;
            Debug.Log($"[DialogueManager] Moving to next node '{currentNode.name}'.");
            yield return RunNode();
        }
        else
        {
            Debug.Log("[DialogueManager] currentNode is null after advancing. Ending dialogue.");
            if (pendingInteractor != null && pendingInteractor.dialogueAsset == currentDialogue)
            {
                pendingInteractor.ShowLockedFeedback();
                pendingInteractor = null;
            }
            EndDialogue();
            yield break;
        }
    }

    // Helper: invoke onExit and run exitActions safely
    private void ExitNodeAndApplyActions(DialogueNode node)
    {
        if (node == null) return;

        try
        {
            Debug.Log($"[DialogueManager] onExitNode Invoke for '{node.name}'");
            node.onExitNode?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Exception while invoking onExitNode for {node.name}: {ex.Message}");
        }

        if (node.exitActions != null && node.exitActions.Count > 0)
        {
            try
            {
                Debug.Log($"[DialogueManager] Applying {node.exitActions.Count} exit action(s) for node '{node.name}'.");
                gameState.ApplyActions(node.exitActions);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Exception applying exit actions on node {node.name}: {ex.Message}");
            }
        }
    }

    // Fallback helpers for ensuring cleanup if the UI never fires its onHideComplete event
    private Coroutine hideFallbackCoroutine;
    private bool hideWaitingForComplete = false;

    private void EndDialogue()
    {
        Debug.Log("[DialogueManager] EndDialogue called. currentDialogue=" + (currentDialogue ? currentDialogue.name : "null") +
              " currentNode=" + (currentNode ? currentNode.name : "null"));
        
        if (dialogueUI != null)
        {
            dialogueUI.onHideComplete.AddListener(HandleOnHideComplete);
            dialogueUI.Hide();

            hideWaitingForComplete = true;
            if (hideFallbackCoroutine != null) StopCoroutine(hideFallbackCoroutine);
            hideFallbackCoroutine = StartCoroutine(HideFallbackCoroutine());
        }
        else
        {
            CleanupAfterDialogue();
        }
    }

    private IEnumerator HideFallbackCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (hideWaitingForComplete)
        {
            Debug.LogWarning("[DialogueManager] onHideComplete did not fire within timeout — forcing cleanup.");
            if (dialogueUI != null)
            {
                dialogueUI.onHideComplete.RemoveListener(HandleOnHideComplete);
            }
            hideWaitingForComplete = false;
            hideFallbackCoroutine = null;
            CleanupAfterDialogue();
        }
    }

    private void HandleOnHideComplete()
    {
        if (dialogueUI != null)
        {
            dialogueUI.onHideComplete.RemoveListener(HandleOnHideComplete);
        }

        hideWaitingForComplete = false;
        if (hideFallbackCoroutine != null)
        {
            StopCoroutine(hideFallbackCoroutine);
            hideFallbackCoroutine = null;
        }

        CleanupAfterDialogue();
    }

    private void CleanupAfterDialogue()
    {
        try
        {
            onDialogueEnd?.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Exception while invoking onDialogueEnd: {ex.Message}");
        }

        currentDialogue = null;
        currentNode = null;
        debugCurrentNode = null;
        pendingInteractor = null;
        if (debugAvailableChoices != null)
            debugAvailableChoices.Clear();
    }

    // Save/load API
    public string SaveGameState() => gameState.ToJson();
    public void LoadGameState(string json) => gameState.FromJson(json);

    public string SaveDialogueProgress(Dialogue dialogue) => dialogue.ToJson();
    public void LoadDialogueProgress(Dialogue dialogue, string json) => dialogue.FromJson(json);
}