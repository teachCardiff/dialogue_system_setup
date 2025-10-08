// StartQuestConsequence.cs
using UnityEngine;

[CreateAssetMenu(fileName = "StartQuestConsequence", menuName = "Dialogue/Consequences/StartQuest", order = 3)]
public class StartQuestConsequence : Consequence
{
    public Quest questToStart;
    
    public override void Execute(GameState gameState)
    {
        if (gameState.IsQuestCompleted(questToStart.questName))
        {
            Debug.Log($"Skipped starting {questToStart.questName}: Already completed.");
            return;
        }
        
        gameState.StartQuest(questToStart);
        Debug.Log($"Started quest: {questToStart.questName}");
    }
}