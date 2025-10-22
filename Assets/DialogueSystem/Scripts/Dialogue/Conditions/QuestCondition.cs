// QuestConditionType.cs
using UnityEngine;

public enum QuestConditionType { Started, InProgress, Completed, NotStarted }

// GenericQuestCondition.cs
[CreateAssetMenu(menuName = "Dialogue/Conditions/QuestCondition")]
public class QuestCondition : Condition
{
    public Quest quest;
    //public string questName;
    public QuestConditionType conditionType;

    public override bool IsMet(GameState gameState)
    {
        var currentQuest = gameState.GetQuest(quest.questName);
        bool isCompleted = gameState.IsQuestCompleted(quest.questName);
        bool isStarted = gameState.IsQuestStarted(quest.questName);

        bool result = false;

        switch (conditionType)
        {
            case QuestConditionType.NotStarted:
                result = !isStarted;
                break;
            case QuestConditionType.Started:
                result = isStarted;
                break;
            case QuestConditionType.InProgress:
                result = currentQuest != null && currentQuest.currentStatus == Quest.Status.InProgress;
                break;
            case QuestConditionType.Completed:
                result = isCompleted;
                break;
        }

        if (currentQuest != null)
        {
            DestroyImmediate(currentQuest); // Clean up temp instance to avoid leaks.
        }

        return result;
    }
}