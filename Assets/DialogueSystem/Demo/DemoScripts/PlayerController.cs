using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in units per second")]
    public float moveSpeed = 5f;

    [Tooltip("Move action (Vector2). If empty a simple keyboard fallback (WASD/Arrows) will be used.")]
    public InputActionReference moveAction;

    [Header("Components")]
    public Rigidbody2D rb;
    public Animator animator;

    [Header("Game State")]
    [Tooltip("Optional GameState ScriptableObject to reset via input (clears variables and quests)")]
    public GameState gameState;

    [Header("Debug / Inputs")]
    [Tooltip("Input action reference to trigger a full game state reset")]
    public InputActionReference resetAction;

    // cached PlayerInput if present (to decide whether to rely on OnMove callbacks)
    PlayerInput playerInput;

    // current raw input read each frame
    Vector2 currentInput = Vector2.zero;
    // processed movement vector constrained to axis-aligned cardinal directions
    Vector2 moveVector = Vector2.zero;

    void OnEnable()
    {
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Enable();
        if (resetAction != null && resetAction.action != null)
            resetAction.action.Enable();
    }

    void OnDisable()
    {
        if (moveAction != null && moveAction.action != null)
            moveAction.action.Disable();
        if (resetAction != null && resetAction.action != null)
            resetAction.action.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // convenience: try to auto-populate common components from children when started
        if (rb == null) rb = GetComponentInChildren<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        playerInput = GetComponent<PlayerInput>();
    }

    // Update is called once per frame
    void Update()
    {
        // read input (new input system) or fallback to keyboard
        if (moveAction != null && moveAction.action != null)
        {
            currentInput = moveAction.action.ReadValue<Vector2>();
        }
        else if (playerInput != null)
        {
            // Using PlayerInput message callbacks (OnMove) to populate currentInput
            // Do nothing here; currentInput is updated in OnMove(InputValue)
        }
        else
        {
            // lightweight keyboard fallback for demos (requires Input System package)
            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = 0f;
                float y = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x = -1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x = 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y = -1f;
                currentInput = new Vector2(x, y);
            }
            else
            {
                currentInput = Vector2.zero;
            }
        }

        // Disallow diagonal movement: pick dominant axis by absolute value and keep its sign
        if (currentInput.sqrMagnitude > 0f)
        {
            if (Mathf.Abs(currentInput.x) > Mathf.Abs(currentInput.y))
            {
                moveVector = new Vector2(Mathf.Sign(currentInput.x), 0f);
            }
            else
            {
                moveVector = new Vector2(0f, Mathf.Sign(currentInput.y));
            }
        }
        else
        {
            moveVector = Vector2.zero;
        }

        // update animator parameters used by a 2D blend tree
        if (animator != null)
        {
            animator.SetFloat("inputX", moveVector.x);
            animator.SetFloat("inputY", moveVector.y);
        }
    }

    // InputSystem message callback when using PlayerInput (send messages behavior)
    public void OnMove(InputValue input)
    {
        if (input == null) return;
        currentInput = input.Get<Vector2>();
    }

    // Optional InputSystem callback or direct hook for reset input
    public void OnReset(InputValue input)
    {
        if (input == null) return;
        if (input.isPressed)
        {
            ResetGameState();
        }
    }

    // Programmatic reset (also called by input action if wired via C#)
    public void ResetGameState()
    {
        if (gameState == null) return;

        gameState.intVariables.Clear();
        gameState.boolVariables.Clear();
        gameState.stringVariables.Clear();
        gameState.activeQuests.Clear();
        gameState.completedQuests.Clear();
        gameState.onStateChanged?.Invoke();

        Debug.Log("GameState reset by PlayerController input.");
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            // set velocity directly for crisp arcade feel
            rb.linearVelocity = moveVector * moveSpeed;
        }
        else
        {
            // fallback to transform-based movement when no Rigidbody2D attached
            transform.Translate(moveVector * moveSpeed * Time.fixedDeltaTime);
        }
    }

    // Optional helper to stop the player immediately
    public void StopMovement()
    {
        moveVector = Vector2.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }
}
