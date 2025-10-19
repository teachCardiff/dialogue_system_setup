using UnityEditor;
using UnityEngine;

namespace DialogueSystem.Editor
{
    public class DialogueDefaultsWindow : EditorWindow
    {
        private Dialogue selectedDialogue;
        private Vector2 scroll;
        private bool applyOnlyIfMissing = true;

        [MenuItem("Window/Dialogue Defaults")]
        public static void OpenWindow()
        {
            var w = GetWindow<DialogueDefaultsWindow>("Dialogue Defaults");
            w.Show();
        }

        private void OnEnable()
        {
            // If user has a Dialogue asset selected, load it
            if (Selection.activeObject is Dialogue d)
            {
                selectedDialogue = d;
                EditorUtility.SetDirty(selectedDialogue);
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is Dialogue d)
            {
                selectedDialogue = d;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dialogue Defaults", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            selectedDialogue = EditorGUILayout.ObjectField("Dialogue Asset", selectedDialogue, typeof(Dialogue), false) as Dialogue;
            if (selectedDialogue == null)
            {
                EditorGUILayout.HelpBox("Select a Dialogue asset to edit its defaults.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Defaults", EditorStyles.boldLabel);
            var newSpeaker = EditorGUILayout.ObjectField("Default Speaker", selectedDialogue.defaultSpeaker, typeof(Character), false) as Character;
            var newListener = EditorGUILayout.ObjectField("Default Listener", selectedDialogue.defaultListener, typeof(Character), false) as Character;

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedDialogue, "Edit Dialogue Defaults");
                selectedDialogue.defaultSpeaker = newSpeaker;
                selectedDialogue.defaultListener = newListener;
                EditorUtility.SetDirty(selectedDialogue);
            }

            EditorGUILayout.Space();
            applyOnlyIfMissing = EditorGUILayout.ToggleLeft("Only apply to nodes missing a speaker/listener", applyOnlyIfMissing);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply To Existing Nodes"))
            {
                if (selectedDialogue != null)
                {
                    Undo.RegisterCompleteObjectUndo(selectedDialogue, "Apply Defaults to Nodes");
                    int applied = 0;
                    foreach (var n in selectedDialogue.nodes)
                    {
                        bool changed = false;
                        if (!applyOnlyIfMissing || n.speakerCharacter == null)
                        {
                            if (selectedDialogue.defaultSpeaker != null)
                            {
                                n.speakerCharacter = selectedDialogue.defaultSpeaker;
                                n.speakerName = selectedDialogue.defaultSpeaker.npcName;
                                if (string.IsNullOrEmpty(n.speakerExpression)) n.speakerExpression = "Default";
                                changed = true;
                            }
                        }
                        if (!applyOnlyIfMissing || n.listenerCharacter == null)
                        {
                            if (selectedDialogue.defaultListener != null)
                            {
                                n.listenerCharacter = selectedDialogue.defaultListener;
                                changed = true;
                            }
                        }
                        if (changed) { EditorUtility.SetDirty(n); applied++; }
                    }
                    EditorUtility.SetDirty(selectedDialogue);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Applied defaults to {applied} nodes.");
                }
            }

            if (GUILayout.Button("Refresh From Asset"))
            {
                // reload values from asset
                EditorUtility.SetDirty(selectedDialogue);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tip: You can also open a Dialogue in the Dialogue Editor. This window works independently of the Dialogue Editor toolbar.", EditorStyles.helpBox);
        }
    }
}
