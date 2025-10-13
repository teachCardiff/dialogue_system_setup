// QuestConditionType.cs
using UnityEngine;

public enum QuestConditionType { Started, InProgress, Completed, NotStarted }

// GenericQuestCondition.cs
[CreateAssetMenu(menuName = "Dialogue/Conditions/GenericQuestCondition")]
public class GenericQuestCondition : Condition
{
    public string questName;
    public QuestConditionType conditionType;

    public override bool IsMet(GameState gameState)
    {
        var quest = gameState.GetQuest(questName);
        if (quest == null) return false;
        switch (conditionType)
        {
            case QuestConditionType.NotStarted: return gameState.IsQuestNotStarted(questName);
            case QuestConditionType.Started: return quest.currentStatus != Quest.Status.NotStarted;
            case QuestConditionType.InProgress: return quest.currentStatus == Quest.Status.InProgress;
            case QuestConditionType.Completed: return quest.currentStatus == Quest.Status.Completed;
            default: return false;
        }
    }
}