using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
        debugCurrentNode = currentNode;
        currentNode.onEnterNode?.Invoke();
        print("Running the node");
        // Auto-branch if conditions met (before showing this node)
        if (currentNode.conditionalBranches.Count > 0)
        {
            foreach (var branch in currentNode.conditionalBranches)
            {
                if (branch.condition != null && branch.condition.IsMet(gameState))
                {
                    print("Skip hub node");
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
                EndDialogue();
                yield break;
            }
        }

        dialogueUI.ShowDialogue(currentNode.speakerName, currentNode.dialogueText);

        // Wait for typewriter to finish or for user to skip
        while (dialogueUI.dialogueText.text != currentNode.dialogueText)
        {
            print("Can skip");
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) // TODO: Update to integrate new input system
            {
                
                dialogueUI.SkipTypewriter();
            }
            yield return null;
        }

        if (currentNode.IsEndNode)
        {
            yield return new WaitUntil(() => dialogueUI.IsNextPressed()); // Wait for player to advance
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

            currentNode = selected.targetNode;
            dialogueUI.ClearChoices();
        }
        else
        {
            yield return new WaitUntil(() => dialogueUI.IsNextPressed());
            currentNode = currentNode.nextNode;
        }

        currentNode.onExitNode?.Invoke();
        currentDialogue.currentNode = currentNode; // Update progress
        currentDialogue.visitedNodes[currentNode.nodeId] = true;

        yield return RunNode(); // Recurse
    }

    private void EndDialogue()
    {
        print("End of dialogue");
        //dialogueUI.nextPressed = false; // Moved to DialogueUI.Hide()
        dialogueUI.Hide();
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