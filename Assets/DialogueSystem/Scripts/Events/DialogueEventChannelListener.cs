using UnityEngine;
using UnityEngine.Events;

// Scene listener that subscribes to a VoidEventChannel and exposes a UnityEvent for designers.
// Allows chaining multiple responses (load scene, enable objects, play timeline, etc.).
public class DialogueEventChannelListener : MonoBehaviour
{
    [Header("Channel")] public DialogueEventChannel channel;
    [Header("Responses")] public UnityEvent onRaised;
    [Header("Options")] public bool log = false;

    private void OnEnable()
    {
        if (channel != null) channel.Raised += HandleRaised;
    }

    private void OnDisable()
    {
        if (channel != null) channel.Raised -= HandleRaised;
    }

    private void HandleRaised()
    {
        if (log) Debug.Log($"[VoidEventChannelListener] Event Raised from channel '{channel.name}'.");
        onRaised?.Invoke();
    }
}
