using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Custom inspector for DialogueUI to help with animation events
[CustomEditor(typeof(DialogueUI))]
public class DialogueUIEditor : Editor
{
    SerializedProperty animatorProp;

    private void OnEnable()
    {
        // serializedObject/target can be null during domain reloads or when Unity creates
        // editors for assets that are temporarily missing. Guard to avoid exceptions.
        if (targets == null || targets.Length == 0 || targets[0] == null)
        {
            animatorProp = null;
            return;
        }

        try
        {
            animatorProp = serializedObject.FindProperty("animator");
        }
        catch (System.Exception)
        {
            // If creating the SerializedObject fails for any reason, leave prop null and
            // let the inspector draw default fields safely.
            animatorProp = null;
        }
    }

    public override void OnInspectorGUI()
    {
        // If the serializedObject isn't available, fall back to the default inspector to
        // avoid accessing null properties.
        if (targets == null || targets.Length == 0 || targets[0] == null)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();
        // Draw the default inspector first
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Event Helper", EditorStyles.boldLabel);

        Animator animator = null;
        if (animatorProp != null)
            animator = animatorProp.objectReferenceValue as Animator;
        else
        {
            // If animatorProp is null, attempt to read directly from the target instance
            var runtimeTarget = target as DialogueUI;
            if (runtimeTarget != null)
                animator = runtimeTarget.GetComponent<Animator>() ?? runtimeTarget.GetType().GetField("animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(runtimeTarget) as Animator;
        }
        if (animator == null)
        {
            EditorGUILayout.HelpBox("No Animator assigned. Assign an Animator to use animation events for show/hide.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        var controller = animator.runtimeAnimatorController;
        if (controller == null)
        {
            EditorGUILayout.HelpBox("Assigned Animator has no RuntimeAnimatorController.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        var clips = controller.animationClips;
        if (clips == null || clips.Length == 0)
        {
            EditorGUILayout.HelpBox("No AnimationClips found on the AnimatorController.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Check for the presence of the specific event
        List<string> clipsWithEvent = new List<string>();
        foreach (var clip in clips)
        {
            var evts = AnimationUtility.GetAnimationEvents(clip);
            foreach (var e in evts)
            {
                if (e.functionName == "OnHideAnimationComplete")
                {
                    clipsWithEvent.Add(clip.name);
                    break;
                }
            }
        }

        if (clipsWithEvent.Count > 0)
        {
            EditorGUILayout.HelpBox("Found OnHideAnimationComplete in: " + string.Join(", ", clipsWithEvent), MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("No AnimationEvent named 'OnHideAnimationComplete' found in the controller's clips.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Clips (click to ping or add event)", EditorStyles.boldLabel);

        foreach (var clip in clips)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);

            bool hasEvent = false;
            var evts = AnimationUtility.GetAnimationEvents(clip);
            foreach (var e in evts)
            {
                if (e.functionName == "OnHideAnimationComplete")
                {
                    hasEvent = true;
                    break;
                }
            }

            if (hasEvent)
            {
                if (GUILayout.Button("Ping", GUILayout.Width(60)))
                {
                    EditorGUIUtility.PingObject(clip);
                    Selection.activeObject = clip;
                }
            }
            else
            {
                if (GUILayout.Button("Add Event", GUILayout.Width(80)))
                {
                    AddHideEventToClip(clip);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Tip: In the Animation window add an event at the end of your hide animation that calls OnHideAnimationComplete(). This utility can add that event for you.", MessageType.None);

        serializedObject.ApplyModifiedProperties();
    }

    private void AddHideEventToClip(AnimationClip clip)
    {
        if (clip == null) return;

        Undo.RecordObject(clip, "Add OnHideAnimationComplete Event");

        var existing = AnimationUtility.GetAnimationEvents(clip);
        foreach (var e in existing)
        {
            if (e.functionName == "OnHideAnimationComplete")
            {
                Debug.Log("Clip already contains OnHideAnimationComplete event: " + clip.name);
                return;
            }
        }

        var list = new List<AnimationEvent>(existing);
        var newEvt = new AnimationEvent
        {
            functionName = "OnHideAnimationComplete",
            time = Mathf.Max(0f, clip.length)
        };
        list.Add(newEvt);
        AnimationUtility.SetAnimationEvents(clip, list.ToArray());
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        Debug.Log("Added OnHideAnimationComplete event to clip: " + clip.name);
    }
}
