// filepath: Assets/DialogueSystem/Scripts/Editor/DialogueNodeEditor.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueNode))]
public class DialogueNodeEditor : Editor
{
    private static readonly Dictionary<int, bool> foldoutState = new Dictionary<int, bool>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw everything except 'playback'
        var iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
                continue;
            }
            if (iterator.name == "playback")
            {
                // skip here; draw with foldout below
                continue;
            }
            EditorGUILayout.PropertyField(iterator, true);
        }

        // Collapsible group for overrides
        var playbackProp = serializedObject.FindProperty("playback");
        if (playbackProp != null)
        {
            int id = target.GetInstanceID();
            if (!foldoutState.ContainsKey(id)) foldoutState[id] = false;
            foldoutState[id] = EditorGUILayout.Foldout(foldoutState[id], new GUIContent("Audio & Flow (Overrides)"), true);
            if (foldoutState[id])
            {
                EditorGUI.indentLevel++;
                var voiceClip = playbackProp.FindPropertyRelative("voiceClip");
                var voiceVolume = playbackProp.FindPropertyRelative("voiceVolume");
                var overrideWaitMode = playbackProp.FindPropertyRelative("overrideWaitMode");
                var waitMode = playbackProp.FindPropertyRelative("waitMode");
                var overrideAuto = playbackProp.FindPropertyRelative("overrideAutoAdvance");
                var autoAdvance = playbackProp.FindPropertyRelative("autoAdvance");
                var autoDelay = playbackProp.FindPropertyRelative("autoAdvanceDelay");
                var allowSkipAudio = playbackProp.FindPropertyRelative("allowSkipAudio");
                var stopVoiceOnExit = playbackProp.FindPropertyRelative("stopVoiceOnExit");
                var matchType = playbackProp.FindPropertyRelative("matchTypewriterToAudio");

                EditorGUILayout.PropertyField(voiceClip);
                EditorGUILayout.Slider(voiceVolume, 0f, 1f);
                EditorGUILayout.PropertyField(overrideWaitMode);
                using (new EditorGUI.DisabledScope(!overrideWaitMode.boolValue))
                {
                    EditorGUILayout.PropertyField(waitMode);
                }
                EditorGUILayout.Space(4);

                EditorGUILayout.PropertyField(overrideAuto);
                if (overrideAuto.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(autoAdvance);
                    EditorGUILayout.PropertyField(autoDelay);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(allowSkipAudio);
                EditorGUILayout.PropertyField(stopVoiceOnExit);
                EditorGUILayout.PropertyField(matchType);
                EditorGUI.indentLevel--;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
