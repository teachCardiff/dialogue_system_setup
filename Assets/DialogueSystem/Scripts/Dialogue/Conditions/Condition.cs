using UnityEngine;

/// <summary>
/// Abstract ScriptableObject for conditions (e.g., check stat > value).
/// Designers create instances and attach to choices.
/// Extensible: Subclass for custom logic.
/// </summary>
public abstract class Condition : ScriptableObject
{
    public abstract bool IsMet(GameState gameState);
}