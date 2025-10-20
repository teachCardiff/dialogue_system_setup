using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
#endif

[DisallowMultipleComponent]
public class DialogueInteract : MonoBehaviour
{
    public enum TriggerMode
    {
        KeyPress,           // legacy KeyCode check in Update
        InputAction,        // use Input System InputActionReference
        OnTriggerEnter,     // requires a trigger collider on this object
        ProximityAndPress,  // requires player to be in trigger and press key/action
        Automatic,          // auto start when conditions met (e.g., OnEnable)
        RemoteCall          // only triggered via public call
    }

    [Header("Dialogue")]
    public Dialogue dialogueAsset;

    [Header("Trigger")]
    public TriggerMode triggerMode = TriggerMode.KeyPress;
    public bool singleUse = true;

    [Tooltip("Key used for legacy KeyPress or ProximityAndPress")] public KeyCode triggerKey = KeyCode.Space;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Optional InputAction (new input system) to trigger the dialogue")] public InputActionReference triggerAction;
#endif

    [Header("Player Filter")]
    [Tooltip("If true the collider/tag/layer checks will be filtered to player only")] public bool requirePlayerTag = true;
    [Tooltip("Tag to consider as player when filtering triggers")] public string playerTag = "Player";
    [Tooltip("Layer mask for proximity checks (optional)")] public LayerMask playerLayer = ~0;

    [Header("Physics")]
    [Tooltip("If true the interact uses 2D trigger callbacks (OnTriggerEnter2D/OnTriggerExit2D) instead of 3D.")]
    public bool use2D = false;

    [Header("Proximity")]
    [Tooltip("If using Proximity/Automatic you can provide a trigger collider, otherwise uses distance check against a Player transform if provided.")]
    public Transform playerTransform;

    [Header("Events")]
    public UnityEvent onTriggered; // invoked when the interaction happens (before starting dialogue)

    // internal
    private bool hasBeenUsed = false;
    private bool playerInRange = false;

#if ENABLE_INPUT_SYSTEM
    private bool actionSubscribed = false;
#endif

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        TrySubscribeInputAction();
#endif
        if (triggerMode == TriggerMode.Automatic)
        {
            // small delay allow other init to complete
            Invoke(nameof(TriggerDialogue), 0.01f);
        }
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        TryUnsubscribeInputAction();
#endif
    }

    private void Update()
    {
        if (hasBeenUsed && singleUse) return;

        switch (triggerMode)
        {
            case TriggerMode.KeyPress:
                if (Input.GetKeyDown(triggerKey)) TriggerDialogue();
                break;
#if ENABLE_INPUT_SYSTEM
            case TriggerMode.InputAction:
                // handled by input action callback
                break;
#endif
            case TriggerMode.ProximityAndPress:
                if (playerInRange)
                {
#if ENABLE_INPUT_SYSTEM
                    if (triggerAction != null && triggerAction.action != null)
                    {
                        // InputAction will call TriggerDialogue via callback
                    }
                    else
#endif
                    if (Input.GetKeyDown(triggerKey))
                    {
                        TriggerDialogue();
                    }
                }
                break;
            default:
                break;
        }
    }

    // When using trigger collider-based proximity (3D)
    private void OnTriggerEnter(Collider other)
    {
        if (use2D) return; // ignore 3D callbacks when using 2D mode
        if (triggerMode != TriggerMode.OnTriggerEnter && triggerMode != TriggerMode.ProximityAndPress) return;
        if (!IsValidPlayer(other.gameObject)) return;

        playerInRange = true;

        if (triggerMode == TriggerMode.OnTriggerEnter)
        {
            TriggerDialogue();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (use2D) return; // ignore 3D callbacks when using 2D mode
        if (triggerMode != TriggerMode.OnTriggerEnter && triggerMode != TriggerMode.ProximityAndPress) return;
        if (!IsValidPlayer(other.gameObject)) return;
        playerInRange = false;
    }

    // 2D equivalents
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!use2D) return; // only run in 2D mode
        if (triggerMode != TriggerMode.OnTriggerEnter && triggerMode != TriggerMode.ProximityAndPress) return;
        if (!IsValidPlayer(other.gameObject)) return;

        playerInRange = true;

        if (triggerMode == TriggerMode.OnTriggerEnter)
        {
            TriggerDialogue();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!use2D) return; // only run in 2D mode
        if (triggerMode != TriggerMode.OnTriggerEnter && triggerMode != TriggerMode.ProximityAndPress) return;
        if (!IsValidPlayer(other.gameObject)) return;
        playerInRange = false;
    }

    private bool IsValidPlayer(GameObject go)
    {
        if (!requirePlayerTag) return true;
        if (go == null) return false;
        if (!string.IsNullOrEmpty(playerTag) && go.CompareTag(playerTag)) return true;
        // fallback to layer check
        if (((1 << go.layer) & playerLayer) != 0) return true;
        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private void TrySubscribeInputAction()
    {
        if (triggerAction == null || triggerAction.action == null || actionSubscribed) return;
        triggerAction.action.performed += OnInputActionPerformed;
        actionSubscribed = true;
    }

    private void TryUnsubscribeInputAction()
    {
        if (triggerAction == null || triggerAction.action == null || !actionSubscribed) return;
        triggerAction.action.performed -= OnInputActionPerformed;
        actionSubscribed = false;
    }

    private void OnInputActionPerformed(InputAction.CallbackContext ctx)
    {
        if (hasBeenUsed && singleUse) return;
        // In ProximityAndPress mode ensure player in range
        if (triggerMode == TriggerMode.ProximityAndPress && !playerInRange) return;
        if (triggerMode == TriggerMode.InputAction || triggerMode == TriggerMode.ProximityAndPress)
        {
            TriggerDialogue();
        }
    }
#endif

    /// <summary>
    /// Public method to trigger the dialogue. Can be called from other scripts, UI buttons, or animation events.
    /// </summary>
    public void TriggerDialogue()
    {
        if (hasBeenUsed && singleUse) return;
        if (dialogueAsset == null)
        {
            Debug.LogWarning("DialogueInteract: no dialogueAsset assigned.");
            return;
        }

        onTriggered?.Invoke();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialogueAsset, true);
        }
        else
        {
            Debug.LogWarning("DialogueManager.Instance is null. Cannot start dialogue.");
        }

        hasBeenUsed = true;
    }

    // Provide a simple API to reset single-use for reuse
    public void ResetUsage()
    {
        hasBeenUsed = false;
    }

    // Expose remote trigger for designer or other scripts
    public void TriggerDialogueRemote()
    {
        TriggerDialogue();
    }
}
