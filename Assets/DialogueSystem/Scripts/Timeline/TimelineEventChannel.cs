using System;
using UnityEngine;

// ScriptableObject event channel to bridge asset (DialogueNode) events to scene Timeline control.
// Create one asset and reference it from node UnityEvents; no scene objects are referenced directly by nodes.
[CreateAssetMenu(menuName = "Events/Timeline Event Channel", fileName = "TimelineEventChannel")]
public class TimelineEventChannel : ScriptableObject
{
    public event Action PlayRequested;
    public event Action PauseRequested;
    public event Action StopRequested;
    public event Action ResumeRequested; // optional resume (same as Play when paused)

    public void RaisePlay() => PlayRequested?.Invoke();
    public void RaisePause() => PauseRequested?.Invoke();
    public void RaiseStop() => StopRequested?.Invoke();
    public void RaiseResume() => ResumeRequested?.Invoke();
}
