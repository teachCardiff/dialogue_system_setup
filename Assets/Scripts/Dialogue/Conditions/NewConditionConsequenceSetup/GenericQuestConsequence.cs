using UnityEngine;

// QuestConsequenceType.cs
public enum QuestConsequenceType { Start, UpdateProgress, Complete }

// GenericQuestConsequence.cs
[CreateAssetMenu(menuName = "Dialogue/Consequences/GenericQuestConsequence")]
public class GenericQuestConsequence : Consequence
{
    public string questName;
    public QuestConsequenceType consequenceType;
    public int objectiveIndex;
    public int progressDelta;

    public override void Execute(GameState gameState)
    {
        switch (consequenceType)
        {
            case QuestConsequenceType.Start:
                var quest = gameState.GetQuest(questName);
                if (quest != null && quest.currentStatus == Quest.Status.NotStarted)
                    gameState.StartQuest(quest);
                break;
            case QuestConsequenceType.UpdateProgress:
                gameState.UpdateQuestProgress(questName, objectiveIndex, progressDelta);
                break;
            case QuestConsequenceType.Complete:
                gameState.CompleteQuest(questName);
                break;
        }
    }
}
