// CheckQuestCondition.cs
using UnityEngine;

[CreateAssetMenu(fileName = "CheckQuestCondition", menuName = "Dialogue/Conditions/CheckQuest", order = 5)]
public class CheckQuestCondition : Condition
{
    public string questName;
    public Quest.Status requiredStatus; // e.g., InProgress, Completed
    
    public override bool IsMet(GameState gameState)
    {
        var quest = gameState.GetQuest(questName);
        return quest != null && quest.currentStatus == requiredStatus;
    }
}