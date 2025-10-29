// filepath: Assets/DialogueSystem/Scripts/Editor/GameStateInspector.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(GameState))]
public class GameStateInspector : Editor
{
    // Remember foldout states per variable GUID
    private static readonly Dictionary<string, bool> s_Expanded = new Dictionary<string, bool>();

    // Simple drag-and-drop payload key and state
    private const string DnDKey = "GameStateInspector_Variable";
    private Variable dragCandidate;
    private Vector2 dragStartPos;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Default fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("characters"));

        // Variables tree (editable)
        var gs = (GameState)target;
        if (gs.root == null)
        {
            if (GUILayout.Button("Initialize Variables Root"))
            {
                Undo.RecordObject(gs, "Initialize Variables Root");
                gs.root = new VariableGroup { Key = "Root", DisplayName = "Root" };
                gs.root.RebuildParentLinks();
                EditorUtility.SetDirty(gs);
                AssetDatabase.SaveAssets();
            }
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variables", EditorStyles.boldLabel);
            DrawVariableRow(gs.root, 0);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Add", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Int")) AddPrimitive<IntVar>("Player", "NewInt", "New Int");
            if (GUILayout.Button("+ Float")) AddPrimitive<FloatVar>("Player", "NewFloat", "New Float");
            if (GUILayout.Button("+ Bool")) AddPrimitive<BoolVar>("Flags", "NewBool", "New Bool");
            if (GUILayout.Button("+ String")) AddPrimitive<StringVar>("Player", "NewString", "New String");
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Quest")) AddQuest();
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("onStateChanged"));

        serializedObject.ApplyModifiedProperties();
    }

    private void AddPrimitive<T>(string groupPath, string key, string display) where T : Variable, new()
    {
        var gs = (GameState)target;
        if (gs.root == null)
        {
            gs.root = new VariableGroup { Key = "Root", DisplayName = "Root" };
        }
        var group = gs.root.EnsureGroup(groupPath.Split('/'));
        var v = new T();
        v.Key = key;
        v.DisplayName = display;
        Undo.RecordObject(gs, "Add Variable");
        group.AddChild(v);
        gs.onStateChanged?.Invoke();
        EditorUtility.SetDirty(gs);
        AssetDatabase.SaveAssets();
    }

    private void AddQuest()
    {
        var gs = (GameState)target;
        if (gs.root == null)
        {
            gs.root = new VariableGroup { Key = "Root", DisplayName = "Root" };
        }
        var quests = gs.root.EnsureGroup("Quests");
        var q = new QuestVariable { Key = "NewQuest", DisplayName = "New Quest" };
        q.status.value = QuestStatus.NotStarted;
        q.name.value = q.DisplayName; // keep Name and Display in sync initially
        q.EnsureOneObjective();
        Undo.RecordObject(gs, "Add Quest");
        quests.AddChild(q);
        gs.root.RebuildParentLinks();
        gs.onStateChanged?.Invoke();
        EditorUtility.SetDirty(gs);
        AssetDatabase.SaveAssets();
    }

    private static bool HasChildren(Variable v)
    {
        if (v == null) return false;
        foreach (var _ in v.GetChildren()) return true;
        return false;
    }

    private static string GetLabel(Variable v)
    {
        if (v == null) return "(null)";
        // Prefer quest display name from its 'name' field if available
        if (v is QuestVariable q && q.name != null && !string.IsNullOrEmpty(q.name.value))
        {
            return q.name.value;
        }
        var label = string.IsNullOrEmpty(v.DisplayName) ? v.Key : v.DisplayName;
        return string.IsNullOrEmpty(label) ? v.GetType().Name : label;
    }

    private bool GetExpanded(Variable v)
    {
        if (v == null) return false;
        if (!s_Expanded.TryGetValue(v.Id, out var expanded)) expanded = true;
        return expanded;
    }
    private void SetExpanded(Variable v, bool expanded)
    {
        if (v == null) return;
        s_Expanded[v.Id] = expanded;
    }

    private void DrawVariableRow(Variable v, int indent)
    {
        if (v == null) return;
        var gs = (GameState)target;
        string label = GetLabel(v);

        // Prepare a single row rect
        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

        // Group-like variables: draw foldout and recurse
        bool isGroupLike = v is VariableGroup || HasChildren(v);
        EditorGUI.indentLevel = indent;
        if (isGroupLike)
        {
            bool expanded = EditorGUI.Foldout(rowRect, GetExpanded(v), label, true);
            SetExpanded(v, expanded);

            // Drag source for any variable
            HandleDragSource(rowRect, v);
            // Accept as drop target only if actual VariableGroup
            HandleDropTarget(rowRect, v as VariableGroup);

            // Inline metadata editor (rename) when expanded
            if (expanded)
            {
                DrawMetadataEditor(v, indent + 1);

                // Quest-specific controls
                if (v is QuestVariable qVar)
                {
                    EditorGUI.indentLevel = indent + 1;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16f);
                        if (GUILayout.Button("+ Add Objective", GUILayout.Width(140)))
                        {
                            Undo.RecordObject(gs, "Add Objective");
                            var o = new ObjectiveVariable { Key = $"Objective {qVar.objectives.Count + 1}", DisplayName = $"Objective {qVar.objectives.Count + 1}" };
                            o.target.value = 1;
                            o.progress.value = 0;
                            qVar.objectives.Add(o);
                            gs.root.RebuildParentLinks();
                            gs.onStateChanged?.Invoke();
                            EditorUtility.SetDirty(gs);
                            AssetDatabase.SaveAssets();
                        }
                    }
                }

                // Children
                foreach (var child in v.GetChildren())
                {
                    DrawVariableRow(child, indent + 1);
                }
            }
        }
        else
        {
            // Leaf: render an inline editor for the value type
            // Draw prefix label and return the remaining rect for the field
            var fieldRect = EditorGUI.PrefixLabel(rowRect, new GUIContent(label));
            EditorGUI.indentLevel = 0;

            bool changed = false;
            Undo.RecordObject(gs, "Edit Variable Value");

            if (v is IntVar iv)
            {
                EditorGUI.BeginChangeCheck();
                int nv = EditorGUI.IntField(fieldRect, iv.value);
                changed = EditorGUI.EndChangeCheck();
                if (changed) iv.value = nv;
            }
            else if (v is FloatVar fv)
            {
                EditorGUI.BeginChangeCheck();
                float nv = EditorGUI.FloatField(fieldRect, fv.value);
                changed = EditorGUI.EndChangeCheck();
                if (changed) fv.value = nv;
            }
            else if (v is BoolVar bv)
            {
                EditorGUI.BeginChangeCheck();
                bool nv = EditorGUI.Toggle(fieldRect, bv.value);
                changed = EditorGUI.EndChangeCheck();
                if (changed) bv.value = nv;
            }
            else if (v is StringVar sv)
            {
                EditorGUI.BeginChangeCheck();
                string nv = EditorGUI.TextField(fieldRect, sv.value);
                bool localChanged = EditorGUI.EndChangeCheck();
                if (localChanged)
                {
                    sv.value = nv;
                    changed = true;

                    // If this string is the Quest's 'name', sync the parent quest's Key/Display
                    var parentQuest = sv.Parent as QuestVariable;
                    if (parentQuest != null && string.Equals(sv.Key, "name", System.StringComparison.OrdinalIgnoreCase))
                    {
                        parentQuest.DisplayName = nv;
                        parentQuest.Key = SanitizeKey(nv);
                    }
                }
            }
            else if (v is QuestVariable.EnumQuestStatus qstat)
            {
                EditorGUI.BeginChangeCheck();
                var nv = (QuestStatus)EditorGUI.EnumPopup(fieldRect, qstat.value);
                changed = EditorGUI.EndChangeCheck();
                if (changed) qstat.value = nv;
            }
            else
            {
                // Unknown leaf type
                EditorGUI.LabelField(fieldRect, v.ValueType != null ? v.ValueType.Name : "");
            }

            if (changed)
            {
                gs.onStateChanged?.Invoke();
                EditorUtility.SetDirty(gs);
                AssetDatabase.SaveAssets();
            }

            // Extra: metadata editor under leaf for renaming
            DrawMetadataEditor(v, indent + 1);

            // Enable dragging from leaf rows too
            HandleDragSource(rowRect, v);
        }

        EditorGUI.indentLevel = 0;
    }

    private void DrawMetadataEditor(Variable v, int indent)
    {
        var gs = (GameState)target;
        EditorGUI.indentLevel = indent;
        using (new EditorGUILayout.VerticalScope())
        {
            // Key
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Key");
                EditorGUI.BeginChangeCheck();
                string newKey = EditorGUILayout.DelayedTextField(v.Key);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(gs, "Rename Variable Key");
                    v.Key = newKey;
                    gs.onStateChanged?.Invoke();
                    EditorUtility.SetDirty(gs);
                }
            }
            // Display Name
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Display");
                EditorGUI.BeginChangeCheck();
                string newDisplay = EditorGUILayout.DelayedTextField(v.DisplayName);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(gs, "Rename Variable Display");
                    v.DisplayName = newDisplay;
                    gs.onStateChanged?.Invoke();
                    EditorUtility.SetDirty(gs);
                }
            }

            // Remove Objective button (if this is an Objective)
            if (v is ObjectiveVariable ov)
            {
                EditorGUI.indentLevel = indent;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(16f);
                    if (GUILayout.Button("Remove Objective", GUILayout.Width(160)))
                    {
                        var parentQuest = ov.Parent as QuestVariable;
                        if (parentQuest != null)
                        {
                            Undo.RecordObject(gs, "Remove Objective");
                            parentQuest.objectives.Remove(ov);
                            gs.root.RebuildParentLinks();
                            gs.onStateChanged?.Invoke();
                            EditorUtility.SetDirty(gs);
                            AssetDatabase.SaveAssets();
                            GUI.changed = true;
                            return; // stop drawing removed item
                        }
                    }
                }
            }
        }
    }

    private void HandleDragSource(Rect rect, Variable v)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && e.button == 0)
        {
            dragCandidate = v;
            dragStartPos = e.mousePosition;
        }
        else if (e.type == EventType.MouseDrag && dragCandidate == v)
        {
            if ((e.mousePosition - dragStartPos).sqrMagnitude > 25f)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DnDKey, v);
                DragAndDrop.objectReferences = new Object[] { (GameState)target }; // keep a reference so DnD is non-empty
                DragAndDrop.StartDrag($"Move {GetLabel(v)}");
                dragCandidate = null;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp && dragCandidate == v)
        {
            dragCandidate = null;
        }
    }

    private void HandleDropTarget(Rect rect, VariableGroup targetGroup)
    {
        if (targetGroup == null) return;
        var e = Event.current;
        var payload = DragAndDrop.GetGenericData(DnDKey) as Variable;
        if (payload == null) return;

        if (!rect.Contains(e.mousePosition)) return;

        bool canDrop = CanDrop(payload, targetGroup);
        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = canDrop ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
        {
            if (canDrop)
            {
                DragAndDrop.AcceptDrag();
                MoveVariable(payload, targetGroup);
                DragAndDrop.SetGenericData(DnDKey, null);
            }
            e.Use();
        }
    }

    private static bool CanDrop(Variable dragged, VariableGroup target)
    {
        if (dragged == null || target == null) return false;
        if (dragged == target) return false;
        // Prevent dropping a parent into its descendant
        foreach (var t in VariableGroup.Traverse(dragged))
        {
            if (t == target) return false;
        }
        return true;
    }

    private void MoveVariable(Variable v, VariableGroup newParent)
    {
        var gs = (GameState)target;
        var oldParent = v.Parent as VariableGroup;
        if (oldParent == newParent) return;

        Undo.RecordObject(gs, "Move Variable");
        if (oldParent != null) oldParent.RemoveChild(v);
        newParent.AddChild(v);
        gs.root.RebuildParentLinks();
        gs.onStateChanged?.Invoke();
        EditorUtility.SetDirty(gs);
        AssetDatabase.SaveAssets();
        GUI.changed = true;
    }

    private static string SanitizeKey(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            else if (char.IsWhiteSpace(c)) { /* skip or replace with underscore */ }
        }
        var s = sb.ToString();
        if (string.IsNullOrEmpty(s)) s = "Var";
        return s;
    }
}
