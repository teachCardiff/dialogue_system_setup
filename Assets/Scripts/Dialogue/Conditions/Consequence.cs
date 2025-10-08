using UnityEngine;

/// <summary>
/// Abstract ScriptableObject for consequences (e.g., update stats, trigger events).
/// Designers create instances and assign to choices for code-free effects.
/// Extensible: Subclass for custom logic (e.g., PlaySoundConsequence).
/// </summary>
public abstract class Consequence : ScriptableObject
{
    public abstract void Execute(GameState gameState); // Invoke at runtime
}