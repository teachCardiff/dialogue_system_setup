using UnityEngine;

// QuestConsequenceType.cs
public enum QuestConsequenceType { Start, UpdateProgress, Complete }

// GenericQuestConsequence.cs
[CreateAssetMenu(menuName = "Dialogue/Consequences/QuestConsequence")]
public class QuestConsequence : Consequence
{
    public Quest quest;
    //public string questName;
    public QuestConsequenceType consequenceType;
    public int objectiveIndex;
    public int progressDelta;

    public override void Execute(GameState gameState)
    {
        switch (consequenceType)
        {
            case QuestConsequenceType.Start:
                if (quest != null)
                    gameState.StartQuest(quest);
                else
                    Debug.LogError($"Cannot start quest: No questTemplate assigned in {name}.");
                break;
            case QuestConsequenceType.UpdateProgress:
                gameState.UpdateQuestProgress(quest.questName, objectiveIndex, progressDelta);
                break;
            case QuestConsequenceType.Complete:
                gameState.CompleteQuest(quest.questName);
                break;
        }
    }
}
