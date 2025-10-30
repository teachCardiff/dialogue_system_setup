// filepath: Assets/DialogueSystem/Scripts/Editor/GameStateInspector.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(GameState))]
public class GameStateInspector : Editor
{
    // Remember foldout states per variable GUID
    private static readonly Dictionary<string, bool> s_Expanded = new Dictionary<string, bool>();
    // New: Advanced foldout state per variable GUID
    private static readonly Dictionary<string, bool> s_Advanced = new Dictionary<string, bool>();
    // New: global advanced visibility
    private static bool s_ShowAdvanced = false;

    // New: inline rename state for groups
    private static readonly Dictionary<string, bool> s_Renaming = new Dictionary<string, bool>();
    private static readonly Dictionary<string, string> s_RenameBuffer = new Dictionary<string, string>();

    // Simple drag-and-drop payload key and state
    private const string DnDKey = "GameStateInspector_Variable";
    private Variable dragCandidate;
    private Vector2 dragStartPos;

    // New: objectives foldout state per quest
    private static readonly Dictionary<string, bool> s_ObjectivesExpanded = new Dictionary<string, bool>();

    private void OnEnable()
    {
        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    private void HandleUndoRedo()
    {
        var gs = target as GameState;
        if (gs != null && gs.root != null)
        {
            gs.root.RebuildParentLinks();
            EditorUtility.SetDirty(gs);
            Repaint();
        }
    }

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
            // Hide the visual Root and draw its children at top level
            var snapshot = new List<Variable>();
            foreach (var c in gs.root.GetChildren()) snapshot.Add(c);
            for (int i = 0; i < snapshot.Count; i++)
            {
                DrawVariableRow(snapshot[i], 0);
            }
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

        // Global Advanced toggle
        EditorGUILayout.Space();
        s_ShowAdvanced = EditorGUILayout.ToggleLeft("Show Advanced", s_ShowAdvanced);

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
        // Prefer objective display name from its 'name' field if available
        if (v is ObjectiveVariable ov && ov.name != null && !string.IsNullOrEmpty(ov.name.value))
        {
            return ov.name.value;
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

    private bool GetAdvanced(Variable v)
    {
        if (v == null) return false;
        if (!s_Advanced.TryGetValue(v.Id, out var expanded)) expanded = false;
        return expanded;
    }
    private void SetAdvanced(Variable v, bool expanded)
    {
        if (v == null) return;
        s_Advanced[v.Id] = expanded;
    }

    private bool GetObjectivesExpanded(QuestVariable q)
    {
        if (q == null) return true;
        if (!s_ObjectivesExpanded.TryGetValue(q.Id, out var b)) b = true;
        return b;
    }
    private void SetObjectivesExpanded(QuestVariable q, bool b)
    {
        if (q == null) return;
        s_ObjectivesExpanded[q.Id] = b;
    }

    // Inline rename helpers
    private bool IsRenaming(Variable v)
    {
        if (v == null) return false;
        return s_Renaming.TryGetValue(v.Id, out var b) && b;
    }
    private string GetRenameBuffer(Variable v)
    {
        if (v == null) return string.Empty;
        if (!s_RenameBuffer.TryGetValue(v.Id, out var s)) s = string.IsNullOrEmpty(v.DisplayName) ? v.Key : v.DisplayName;
        return s;
    }
    private void BeginRename(Variable v)
    {
        if (v == null) return;
        s_Renaming[v.Id] = true;
        s_RenameBuffer[v.Id] = string.IsNullOrEmpty(v.DisplayName) ? v.Key : v.DisplayName;
    }
    private void CommitRename(Variable v, string newName)
    {
        if (v == null) return;
        var gs = (GameState)target;
        Undo.RecordObject(gs, "Rename Group");
        v.DisplayName = newName;
        // Keep group keys in sync with display for intuitive paths
        if (v is VariableGroup)
        {
            v.Key = SanitizeKey(newName);
        }
        s_Renaming[v.Id] = false;
        gs.onStateChanged?.Invoke();
        EditorUtility.SetDirty(gs);
        AssetDatabase.SaveAssets();
        GUI.changed = true;
        Repaint();
    }
    private void CancelRename(Variable v)
    {
        if (v == null) return;
        s_Renaming[v.Id] = false;
    }

    private void DrawVariableRow(Variable v, int indent)
    {
        if (v == null) return;
        var gs = (GameState)target;
        string label = GetLabel(v);

        // Prepare a single row rect
        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

        // Reserve a small area on the right for row actions (Up/Down/Delete)
        float actionsWidth = 72f; // three small buttons
        var contentRect = new Rect(rowRect.x, rowRect.y, Mathf.Max(0, rowRect.width - actionsWidth), rowRect.height);
        var actionsRect = new Rect(contentRect.xMax + 4f, rowRect.y, actionsWidth - 4f, rowRect.height);

        // Group-like variables: draw foldout and recurse
        bool isGroupLike = v is VariableGroup || HasChildren(v);
        EditorGUI.indentLevel = indent;
        if (isGroupLike)
        {
            bool isGroup = v is VariableGroup;

            // Use an indented rect for all group content so badges and text align correctly per hierarchy level
            Rect indentedRect = EditorGUI.IndentedRect(contentRect);

            // For groups, reserve a small right-aligned badge area to show child count
            Rect foldoutRect = indentedRect;
            Rect countRect = Rect.zero;
            int directChildCount = 0;
            if (isGroup)
            {
                directChildCount = ((VariableGroup)v).Children != null ? ((VariableGroup)v).Children.Count : 0;
                string countText = directChildCount.ToString();
                var style = EditorStyles.miniLabel;
                Vector2 size = style.CalcSize(new GUIContent(countText));
                float badgeW = Mathf.Max(18f, size.x + 10f);
                countRect = new Rect(indentedRect.xMax - badgeW, indentedRect.y + 2f, badgeW, indentedRect.height - 4f);
                foldoutRect = new Rect(indentedRect.x, indentedRect.y, Mathf.Max(0, indentedRect.width - badgeW - 6f), indentedRect.height);
            }

            // Context menu for groups (Rename)
            var evt = Event.current;
            if (isGroup && evt.type == EventType.ContextClick && indentedRect.Contains(evt.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Rename"), false, () => { BeginRename(v); Repaint(); });
                menu.ShowAsContext();
                evt.Use();
            }

            bool renaming = isGroup && IsRenaming(v);

            // Draw foldout: if renaming, only make the arrow clickable (not the label area)
            bool expanded;
            if (renaming)
            {
                var arrowRect = new Rect(foldoutRect.x, foldoutRect.y, 16f, foldoutRect.height);
                expanded = EditorGUI.Foldout(arrowRect, GetExpanded(v), GUIContent.none, false);
                SetExpanded(v, expanded);

                // Inline rename field placed next to the arrow, not overlapping it
                var tfRect = new Rect(arrowRect.xMax + 2f, foldoutRect.y, Mathf.Max(0, foldoutRect.width - (arrowRect.width + 2f)), foldoutRect.height);

                // Handle commit/cancel keys while renaming
                if (evt.type == EventType.KeyDown)
                {
                    if (evt.keyCode == KeyCode.Escape)
                    {
                        CancelRename(v);
                        Repaint();
                        evt.Use();
                    }
                    // Do not intercept Return/Enter here; let DelayedTextField commit the change
                }

                string buf = GetRenameBuffer(v);
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName("RenameField");
                buf = EditorGUI.DelayedTextField(tfRect, buf);
                // Ensure focus stays on the rename field while renaming
                if (GUI.GetNameOfFocusedControl() != "RenameField")
                    EditorGUI.FocusTextInControl("RenameField");
                if (EditorGUI.EndChangeCheck())
                {
                    s_RenameBuffer[v.Id] = buf;
                    // Commit on delayed edit (enter or focus loss)
                    CommitRename(v, buf);
                }
            }
            else
            {
                expanded = EditorGUI.Foldout(foldoutRect, GetExpanded(v), label, true);
                SetExpanded(v, expanded);
            }

            // Draw child count badge (right-aligned) for all VariableGroup rows
            if (isGroup)
            {
                // Subtle pill background for visibility in both light and dark themes
                var bgCol = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.08f);
                EditorGUI.DrawRect(countRect, bgCol);
                var countStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 2, 0, 0),
                    clipping = TextClipping.Clip,
                };
                countStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.9f) : new Color(0f, 0f, 0f, 0.9f);
                GUI.Label(countRect, directChildCount.ToString(), countStyle);
            }

            // Drag source and drop target against the contentRect for better hit area
            if (!renaming)
            {
                HandleDragSource(contentRect, v);
                HandleDropTarget(indentedRect, v as VariableGroup);
            }

            // Row actions (Up/Down/Delete) for non-root items
            DrawRowActions(actionsRect, v);

            // Re-draw count text after actions to ensure visibility on top of other controls
            if (isGroup)
            {
                var countStyleTop = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(4, 2, 0, 0),
                    clipping = TextClipping.Clip,
                };
                countStyleTop.normal.textColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.95f) : new Color(0f, 0f, 0f, 0.95f);
                GUI.Label(countRect, directChildCount.ToString(), countStyleTop);
            }

            if (expanded)
            {
                if (v is QuestVariable qVar)
                {
                    // Quest custom authoring block
                    DrawMetadataEditor(qVar, indent + 1);

                    // Name (indented more than the quest header)
                    EditorGUI.indentLevel = indent + 2;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Name");
                        EditorGUI.BeginChangeCheck();
                        string newName = EditorGUILayout.TextField(qVar.name.value);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(gs, "Edit Quest Name");
                            qVar.name.value = newName;
                            qVar.DisplayName = newName;
                            qVar.Key = SanitizeKey(newName);
                            gs.root.RebuildParentLinks();
                            gs.onStateChanged?.Invoke();
                            EditorUtility.SetDirty(gs);
                        }
                    }

                    // Status (indented more than the quest header)
                    EditorGUI.indentLevel = indent + 2;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("Status");
                        EditorGUI.BeginChangeCheck();
                        var ns = (QuestStatus)EditorGUILayout.EnumPopup(qVar.status.value);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(gs, "Edit Quest Status");
                            qVar.status.value = ns;
                            gs.onStateChanged?.Invoke();
                            EditorUtility.SetDirty(gs);
                        }
                    }

                    // Objectives foldout with add button (indented)
                    EditorGUI.indentLevel = indent + 1;
                    Rect objHeaderRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    Rect objHeaderIndented = EditorGUI.IndentedRect(objHeaderRect);
                    bool objExpanded = GetObjectivesExpanded(qVar);
                    var objFoldRect = new Rect(objHeaderIndented.x, objHeaderIndented.y, objHeaderIndented.width - 28f, objHeaderIndented.height);
                    var objAddRect = new Rect(objHeaderIndented.xMax - 24f, objHeaderIndented.y, 24f, objHeaderIndented.height);
                    objExpanded = EditorGUI.Foldout(objFoldRect, objExpanded, "Objectives", true);
                    SetObjectivesExpanded(qVar, objExpanded);
                    if (GUI.Button(objAddRect, "+"))
                    {
                        Undo.RecordObject(gs, "Add Objective");
                        var obj = new ObjectiveVariable { Key = $"Objective {qVar.objectives.Count + 1}", DisplayName = $"Objective {qVar.objectives.Count + 1}" };
                        obj.name.value = obj.DisplayName;
                        obj.target.value = 1;
                        obj.progress.value = 0;
                        qVar.objectives.Add(obj);
                        gs.root.RebuildParentLinks();
                        gs.onStateChanged?.Invoke();
                        EditorUtility.SetDirty(gs);
                        AssetDatabase.SaveAssets();
                        GUI.changed = true;
                    }

                    if (objExpanded)
                    {
                        // Draw each objective as a child row
                        var objSnapshot = new List<ObjectiveVariable>(qVar.objectives);
                        for (int i = 0; i < objSnapshot.Count; i++)
                        {
                            DrawVariableRow(objSnapshot[i], indent + 2);
                        }
                    }

                    return; // custom quest rendering handled
                }

                // Default rendering for other group-like variables
                DrawMetadataEditor(v, indent + 1);

                // Snapshot children before iterating to avoid collection modified exceptions
                var snapshot = new List<Variable>();
                foreach (var c in v.GetChildren()) snapshot.Add(c);
                for (int i = 0; i < snapshot.Count; i++)
                {
                    DrawVariableRow(snapshot[i], indent + 1);
                }
            }
        }
        else
        {
            // Single-row editor for primitive leaves (Int/Float/Bool/String)
            bool isPrimitive = v is IntVar || v is FloatVar || v is BoolVar || v is StringVar;
            if (isPrimitive)
            {
                var indented = EditorGUI.IndentedRect(contentRect);
                float split = Mathf.Clamp(indented.width * 0.5f, 140f, indented.width - 100f);
                var nameRect = new Rect(indented.x, indented.y, split, indented.height);
                var valueRect = new Rect(indented.x + split + 6f, indented.y, indented.width - split - 6f, indented.height);

                bool changed = false;
                Undo.RecordObject(gs, "Edit Variable");

                // Left: editable display name, with special cases
                bool showEditableName = true;
                string fixedLeftLabel = null;

                if (v is StringVar sv && sv.Parent is QuestVariable && string.Equals(sv.Key, "name", System.StringComparison.OrdinalIgnoreCase))
                {
                    showEditableName = false; fixedLeftLabel = "Name";
                }
                else if (v is StringVar osv && osv.Parent is ObjectiveVariable && string.Equals(osv.Key, "name", System.StringComparison.OrdinalIgnoreCase))
                {
                    showEditableName = false; fixedLeftLabel = "Obj Name";
                }
                else if (v is IntVar ivA && ivA.Parent is ObjectiveVariable &&
                         (string.Equals(ivA.Key, "target", System.StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(ivA.Key, "progress", System.StringComparison.OrdinalIgnoreCase)))
                {
                    showEditableName = false; fixedLeftLabel = char.ToUpper(ivA.Key[0]) + ivA.Key.Substring(1);
                }

                if (showEditableName)
                {
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUI.DelayedTextField(nameRect, v.DisplayName);
                    if (EditorGUI.EndChangeCheck()) { v.DisplayName = newName; changed = true; }
                }
                else
                {
                    EditorGUI.LabelField(nameRect, fixedLeftLabel);
                }

                if (v is IntVar iv)
                {
                    EditorGUI.BeginChangeCheck();
                    int nv = EditorGUI.IntField(valueRect, iv.value);
                    if (EditorGUI.EndChangeCheck()) { iv.value = nv; changed = true; }
                }
                else if (v is FloatVar fv)
                {
                    EditorGUI.BeginChangeCheck();
                    float nv = EditorGUI.FloatField(valueRect, fv.value);
                    if (EditorGUI.EndChangeCheck()) { fv.value = nv; changed = true; }
                }
                else if (v is BoolVar bv)
                {
                    EditorGUI.BeginChangeCheck();
                    bool nv = EditorGUI.Toggle(valueRect, bv.value);
                    if (EditorGUI.EndChangeCheck()) { bv.value = nv; changed = true; }
                }
                else if (v is StringVar sv2)
                {
                    EditorGUI.BeginChangeCheck();
                    string nv = EditorGUI.TextField(valueRect, sv2.value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        sv2.value = nv; changed = true;
                        var parentQuest = sv2.Parent as QuestVariable;
                        if (parentQuest != null && string.Equals(sv2.Key, "name", System.StringComparison.OrdinalIgnoreCase))
                        { parentQuest.DisplayName = nv; parentQuest.Key = SanitizeKey(nv); }
                        var parentObj = sv2.Parent as ObjectiveVariable;
                        if (parentObj != null && string.Equals(sv2.Key, "name", System.StringComparison.OrdinalIgnoreCase))
                        { parentObj.DisplayName = nv; parentObj.Key = SanitizeKey(nv); }
                    }
                }

                if (changed)
                {
                    gs.onStateChanged?.Invoke();
                    EditorUtility.SetDirty(gs);
                    AssetDatabase.SaveAssets();
                }

                HandleDragSource(contentRect, v);
                DrawRowActions(actionsRect, v);
                // Note: non-group leaves have no children to enumerate
            }
            else
            {
                var fieldRect = EditorGUI.PrefixLabel(contentRect, new GUIContent(label));
                EditorGUI.indentLevel = 0;
                bool changed = false;
                Undo.RecordObject(gs, "Edit Variable Value");

                if (v is QuestVariable.EnumQuestStatus qstat)
                {
                    EditorGUI.BeginChangeCheck();
                    var nv = (QuestStatus)EditorGUI.EnumPopup(fieldRect, qstat.value);
                    changed = EditorGUI.EndChangeCheck();
                    if (changed) qstat.value = nv;
                }
                else
                {
                    EditorGUI.LabelField(fieldRect, v.ValueType != null ? v.ValueType.Name : "");
                }

                if (changed)
                {
                    gs.onStateChanged?.Invoke();
                    EditorUtility.SetDirty(gs);
                    AssetDatabase.SaveAssets();
                }

                DrawMetadataEditor(v, indent + 1);
                HandleDragSource(contentRect, v);
                DrawRowActions(actionsRect, v);
            }
        }

        EditorGUI.indentLevel = 0;
    }

    private void DrawMetadataEditor(Variable v, int indent)
    {
        var gs = (GameState)target;
        EditorGUI.indentLevel = indent;
        using (new EditorGUILayout.VerticalScope())
        {
            // Decide whether to show Display field
            bool showDisplay = true;
            if (v is VariableGroup)
            {
                // Groups use right-click rename instead of a Display field
                showDisplay = false;
            }
            else if (v is QuestVariable)
            {
                // Quest display is driven by the Name field
                showDisplay = false;
            }
            else if (v is ObjectiveVariable)
            {
                // Objective header label comes from its Obj Name; hide Display
                showDisplay = false;
            }
            else if (v is StringVar sv && sv.Parent is QuestVariable && string.Equals(sv.Key, "name", System.StringComparison.OrdinalIgnoreCase))
            {
                // The quest's Name display label should remain 'Name'
                showDisplay = false;
            }
            else if (v is StringVar osv && osv.Parent is ObjectiveVariable && string.Equals(osv.Key, "name", System.StringComparison.OrdinalIgnoreCase))
            {
                // Objective name label fixed in the row UI
                showDisplay = false;
            }
            else if (v is IntVar iv && iv.Parent is ObjectiveVariable &&
                     (string.Equals(iv.Key, "target", System.StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(iv.Key, "progress", System.StringComparison.OrdinalIgnoreCase)))
            {
                // Objective Target/Progress labels are fixed
                showDisplay = false;
            }

            if (showDisplay)
            {
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
            }

            // Advanced foldout (Key editing) gated by global toggle
            if (s_ShowAdvanced)
            {
                bool adv = GetAdvanced(v);
                adv = EditorGUILayout.Foldout(adv, "Advanced", true);
                SetAdvanced(v, adv);
                if (adv)
                {
                    EditorGUI.indentLevel = indent + 1;
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
                }
            }

            // Removed: explicit 'Remove Objective' button; use row ✖ instead
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

    private void DrawRowActions(Rect rect, Variable v)
    {
        var gs = (GameState)target;
        var parentGroup = v.Parent as VariableGroup;
        var parentQuest = v.Parent as QuestVariable;

        // Special handling: objective row actions when parent is a QuestVariable
        if (parentQuest != null && v is ObjectiveVariable obj)
        {
            int index = parentQuest.objectives.IndexOf(obj);
            int count = parentQuest.objectives.Count;

            float bw = Mathf.Floor((rect.width - 4f) / 3f);
            var upRect = new Rect(rect.x, rect.y, bw, rect.height);
            var downRect = new Rect(rect.x + bw + 2f, rect.y, bw, rect.height);
            var delRect = new Rect(rect.x + (bw * 2f) + 4f, rect.y, bw, rect.height);

            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUI.Button(upRect, "▲"))
                {
                    Undo.RecordObject(gs, "Move Objective Up");
                    if (index > 0)
                    {
                        var tmp = parentQuest.objectives[index - 1];
                        parentQuest.objectives[index - 1] = obj;
                        parentQuest.objectives[index] = tmp;
                        gs.root.RebuildParentLinks();
                        EditorUtility.SetDirty(gs);
                        GUI.changed = true;
                    }
                }
            }
            using (new EditorGUI.DisabledScope(index >= count - 1))
            {
                if (GUI.Button(downRect, "▼"))
                {
                    Undo.RecordObject(gs, "Move Objective Down");
                    if (index < count - 1)
                    {
                        var tmp = parentQuest.objectives[index + 1];
                        parentQuest.objectives[index + 1] = obj;
                        parentQuest.objectives[index] = tmp;
                        gs.root.RebuildParentLinks();
                        EditorUtility.SetDirty(gs);
                        GUI.changed = true;
                    }
                }
            }
            if (GUI.Button(delRect, "✖"))
            {
                Undo.RecordObject(gs, "Delete Objective");
                parentQuest.objectives.Remove(obj);
                gs.root.RebuildParentLinks();
                gs.onStateChanged?.Invoke();
                EditorUtility.SetDirty(gs);
                AssetDatabase.SaveAssets();
                GUI.changed = true;
            }
            return;
        }

        if (parentGroup == null) return; // root-level or quest children where not handled above

        int idx = parentGroup.IndexOf(v);
        int cnt = parentGroup.Children.Count;

        float bwg = Mathf.Floor((rect.width - 4f) / 3f);
        var upR = new Rect(rect.x, rect.y, bwg, rect.height);
        var downR = new Rect(rect.x + bwg + 2f, rect.y, bwg, rect.height);
        var delR = new Rect(rect.x + (bwg * 2f) + 4f, rect.y, bwg, rect.height);

        using (new EditorGUI.DisabledScope(idx <= 0))
        {
            if (GUI.Button(upR, "▲"))
            {
                Undo.RecordObject(gs, "Move Up");
                if (parentGroup.MoveChild(v, idx - 1))
                {
                    gs.root.RebuildParentLinks();
                    EditorUtility.SetDirty(gs);
                    GUI.changed = true;
                }
            }
        }
        using (new EditorGUI.DisabledScope(idx >= cnt - 1))
        {
            if (GUI.Button(downR, "▼"))
            {
                Undo.RecordObject(gs, "Move Down");
                if (parentGroup.MoveChild(v, idx + 1))
                {
                    gs.root.RebuildParentLinks();
                    EditorUtility.SetDirty(gs);
                    GUI.changed = true;
                }
            }
        }
        if (GUI.Button(delR, "✖"))
        {
            Undo.RecordObject(gs, "Delete Variable");
            parentGroup.RemoveChild(v);
            gs.root.RebuildParentLinks();
            gs.onStateChanged?.Invoke();
            EditorUtility.SetDirty(gs);
            AssetDatabase.SaveAssets();
            GUI.changed = true;
        }
    }
}
