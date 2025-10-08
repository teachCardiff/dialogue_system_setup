using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ScriptableObject representing a full dialogue tree.
/// Contains a list of nodes and a starting node.
/// Supports serialization for save/load.
/// </summary>
[CreateAssetMenu(fileName = "Dialogue", menuName = "Dialogue/Dialogue", order = 2)]
public class Dialogue : ScriptableObject
{
    public DialogueNode startNode;
    public List<DialogueNode> nodes = new List<DialogueNode>();

    // Store node positions for the editor (nodeId -> Rect)
    [System.Serializable]
    public class NodePosition
    {
        public string nodeId;
        public Vector2 position;
    }
    public List<NodePosition> nodePositions = new List<NodePosition>(); // Changed to List for serialization

    // For runtime tracking (saved separately)
    [HideInInspector] public DialogueNode currentNode;
    [HideInInspector] public Dictionary<string, bool> visitedNodes = new Dictionary<string, bool>(); // For conditional repeats, etc.


    /// <summary>
    /// Resets dialogue progress to the start node (for previews/testing).
    /// Call this before starting a dialogue to ensure it begins fresh.
    /// </summary>
    public void ResetProgress()
    {
        currentNode = startNode;
        visitedNodes.Clear();
        EditorUtility.SetDirty(this); // Editor-only, for asset persistence
    }

    // Save/load progress
    public string ToJson()
    {
        // Serialize current node ID and visited
        var data = new SerializableDialogueProgress { currentNodeId = currentNode?.nodeId };
        data.visitedNodeIds = new List<string>(visitedNodes.Keys);
        return JsonUtility.ToJson(data);
    }

    public void FromJson(string json)
    {
        var data = JsonUtility.FromJson<SerializableDialogueProgress>(json);
        currentNode = nodes.Find(n => n.nodeId == data.currentNodeId);
        visitedNodes.Clear();
        foreach (var id in data.visitedNodeIds) visitedNodes[id] = true;
    }

    [System.Serializable]
    private class SerializableDialogueProgress
    {
        public string currentNodeId;
        public List<string> visitedNodeIds;
    }

    // Helper to get/set node positions
    public Vector2 GetNodePosition(string nodeId)
    {
        var pos = nodePositions.Find(np => np.nodeId == nodeId);
        return pos != null ? pos.position : Vector2.zero;
    }

    public void SetNodePosition(string nodeId, Rect rect)
    {
        var pos = nodePositions.Find(np => np.nodeId == nodeId);
        if (pos != null)
        {
            pos.position = rect.position;
        }
        else
        {
            nodePositions.Add(new NodePosition { nodeId = nodeId, position = rect.position });
        }
    }
}