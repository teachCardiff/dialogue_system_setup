// UpdateQuestProgressConsequence.cs
using UnityEngine;

[CreateAssetMenu(fileName = "UpdateQuestProgressConsequence", menuName = "Dialogue/Consequences/UpdateQuestProgress", order = 4)]
public class UpdateQuestProgressConsequence : Consequence
{
    public string questName;
    public int objectiveIndex; // 0-based
    public int progressDelta = 1; // e.g., +1 for collectibles
    
    public override void Execute(GameState gameState)
    {
        var currentProgress = gameState.GetQuest(questName)?.GetObjectiveProgress(objectiveIndex) ?? 0;
        gameState.UpdateQuestProgress(questName, objectiveIndex, currentProgress + progressDelta);
        Debug.Log($"Updated {questName} objective {objectiveIndex} to {currentProgress + progressDelta}");
    }
}