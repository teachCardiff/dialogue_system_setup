using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class DialogueMovementBlocker : MonoBehaviour
{
    [Tooltip("List of components (MonoBehaviours) to disable when dialogue starts.")]
    public List<Behaviour> componentsToToggle = new List<Behaviour>();

#if ENABLE_INPUT_SYSTEM
    [Tooltip("Optional PlayerInput to disable when dialogue starts.")]
    public PlayerInput playerInput;
#endif

    [Tooltip("If true, the component will attempt to SetActive(false) on the GameObject that owns each component instead of disabling the component itself.")]
    public bool disableGameObjectInstead = false;

    [Tooltip("If true and the list is empty, tries to auto-find common movement/input components on the same GameObject (PlayerController, PlayerInput).")]
    public bool autoFindCommon = true;

    [Header("Events")]
    public UnityEvent onBlocked;
    public UnityEvent onUnblocked;

    // Internal state bookkeeping so we can restore previous states
    private Dictionary<Behaviour, bool> previousEnabled = new Dictionary<Behaviour, bool>();
    private Dictionary<GameObject, bool> previousActive = new Dictionary<GameObject, bool>();
    private bool isBlocked = false;

    void OnEnable()
    {
        // Try to subscribe if DialogueManager already exists
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.onDialogueStart.AddListener(HandleOnDialogueStart);
            DialogueManager.Instance.onDialogueEnd.AddListener(HandleOnDialogueEnd);
        }
    }

    void Start()
    {
        // If DialogueManager wasn't present at OnEnable, try to subscribe now
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.onDialogueStart.AddListener(HandleOnDialogueStart);
            DialogueManager.Instance.onDialogueEnd.AddListener(HandleOnDialogueEnd);
        }

        if (autoFindCommon && (componentsToToggle == null || componentsToToggle.Count == 0))
        {
            // Try to auto-populate common components to toggle if present
            var pc = GetComponent<PlayerController>();
            if (pc != null) componentsToToggle.Add(pc);

#if ENABLE_INPUT_SYSTEM
            if (playerInput == null)
            {
                var pi = GetComponent<PlayerInput>();
                if (pi != null) playerInput = pi;
            }
#endif
        }
    }

    void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.onDialogueStart.RemoveListener(HandleOnDialogueStart);
            DialogueManager.Instance.onDialogueEnd.RemoveListener(HandleOnDialogueEnd);
        }

        // Ensure we restore state if the blocker is disabled while blocked
        if (isBlocked)
        {
            Unblock();
        }
    }

    // Public API to trigger block/unblock manually if desired
    public void Block()
    {
        if (isBlocked) return;
        isBlocked = true;

        previousEnabled.Clear();
        previousActive.Clear();

        if (componentsToToggle != null)
        {
            foreach (var comp in componentsToToggle)
            {
                if (comp == null) continue;

                if (disableGameObjectInstead && comp.gameObject != this.gameObject)
                {
                    var go = comp.gameObject;
                    if (!previousActive.ContainsKey(go))
                        previousActive[go] = go.activeSelf;
                    go.SetActive(false);
                }
                else
                {
                    if (!previousEnabled.ContainsKey(comp))
                        previousEnabled[comp] = comp.enabled;
                    comp.enabled = false;
                }
            }
        }

#if ENABLE_INPUT_SYSTEM
        if (playerInput != null)
        {
            // record enabled state via Behaviour for consistency
            var beh = playerInput as Behaviour;
            if (beh != null && !previousEnabled.ContainsKey(beh))
                previousEnabled[beh] = beh.enabled;
            playerInput.enabled = false;
        }
#endif

        try { onBlocked?.Invoke(); } catch (System.Exception ex) { Debug.LogWarning($"Exception in onBlocked: {ex.Message}"); }
    }

    public void Unblock()
    {
        if (!isBlocked) return;
        isBlocked = false;

        // Restore component enabled states
        foreach (var kv in previousEnabled)
        {
            var comp = kv.Key;
            if (comp == null) continue;
            comp.enabled = kv.Value;
        }
        previousEnabled.Clear();

        // Restore GameObject active states
        foreach (var kv in previousActive)
        {
            var go = kv.Key;
            if (go == null) continue;
            go.SetActive(kv.Value);
        }
        previousActive.Clear();

        try { onUnblocked?.Invoke(); } catch (System.Exception ex) { Debug.LogWarning($"Exception in onUnblocked: {ex.Message}"); }
    }

    private void HandleOnDialogueStart()
    {
        Block();
    }

    private void HandleOnDialogueEnd()
    {
        Unblock();
    }
}
