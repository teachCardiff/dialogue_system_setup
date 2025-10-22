using UnityEngine;

/// <summary>
/// Condition to check if an int variable meets a comparison.
/// </summary>
[CreateAssetMenu(fileName = "CheckIntCondition", menuName = "Dialogue/Conditions/CheckInt", order = 4)]
public class CheckIntCondition : Condition
{
    public string variableName;
    public int compareValue;
    public ComparisonType comparison;

    public enum ComparisonType { GreaterThan, LessThan, Equal, NotEqual }

    public override bool IsMet(GameState gameState)
    {
        int value = gameState.GetInt(variableName);
        switch (comparison)
        {
            case ComparisonType.GreaterThan: return value > compareValue;
            case ComparisonType.LessThan: return value < compareValue;
            case ComparisonType.Equal: return value == compareValue;
            case ComparisonType.NotEqual: return value != compareValue;
            default: return false;
        }
    }
}