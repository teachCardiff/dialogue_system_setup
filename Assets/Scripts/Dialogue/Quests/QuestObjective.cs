using UnityEngine;

[CreateAssetMenu(fileName = "QuestObjective", menuName = "Dialogue/QuestObjective", order = 6)]
public class QuestObjective : ScriptableObject
{
    [TextArea(1, 3)] public string description; // e.g., "Collect 3 iron ores"
    public int targetValue = 1; // e.g., 3 for collectibles
    [HideInInspector] public int currentProgress = 0;
    
    public bool IsComplete => currentProgress >= targetValue;
    
    // Helper for UI/display
    public string GetProgressString()
    {
        return $"{currentProgress}/{targetValue}";
    }
}
