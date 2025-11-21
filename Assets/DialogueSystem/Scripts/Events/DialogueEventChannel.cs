using System;
using UnityEngine;

// Generic no-argument event channel for decoupling ScriptableObject dialogue nodes from scene objects.
// Designer workflow:
// 1. Create a VoidEventChannel asset (Right Click > Create > Events > Void Event Channel).
// 2. In a DialogueNode's onEnterNode / onExitNode UnityEvent, add this asset and call Raise().
// 3. In the scene, add a VoidEventChannelListener component, assign the channel, and hook UnityEvent responses.
[CreateAssetMenu(menuName = "Events/Void Event Channel", fileName = "DialogueEventChannel")]
public class DialogueEventChannel : ScriptableObject
{
    public event Action Raised;

    public void Raise() => Raised?.Invoke();
}
