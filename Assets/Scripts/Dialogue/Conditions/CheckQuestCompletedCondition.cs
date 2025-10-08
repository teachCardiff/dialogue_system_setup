using UnityEngine;

[CreateAssetMenu(fileName = "CheckQuestCompletedCondition", menuName = "Dialogue/Conditions/CheckQuestCompleted", order = 6)]
public class CheckQuestCompletedCondition : Condition
{
    public string questName; // e.g., "FetchSword"
    
    public override bool IsMet(GameState gameState)
    {
        bool isCompleted = gameState.IsQuestCompleted(questName);
        Debug.Log($"CheckQuestCompleted: {questName} is completed? {isCompleted}"); // Temp for testing
        return isCompleted;
    }
}