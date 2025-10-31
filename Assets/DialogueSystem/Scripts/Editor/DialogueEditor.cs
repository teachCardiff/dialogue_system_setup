using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// Custom Editor Window for visual dialogue editing.
    /// Provides a node-based interface for designers to create/edit dialogue trees.
    /// Open via Menu: Window > Dialogue Editor.
    /// Supports creating nodes at click position, edge-drop node creation, immediate node display, persistent node positions, choice deletion, and node deletion (via context menu or Delete key).
    /// </summary>
    public class DialogueEditor : EditorWindow
    {
        public Dialogue selectedDialogue;
        private DialogueGraphView graphView;
        private Vector2 lastMousePosition; // Track for node-right-click creation
    // IMGUI toolbar state
    private bool applyOnlyIfMissing = true;
        // When true, OnGraphChanged will ignore element removals to avoid deleting assets while we
        // programmatically clear the graph (for example when switching selected Dialogue assets).
        private bool suppressGraphViewDeletion = false;
    // Diagnostic dry-run: when true scheduled removals will only log and not actually remove assets
    private static bool diagnosticsDryRun = false;

        [MenuItem("Window/Dialogue Editor")]
        public static void Open()
        {
            GetWindow<DialogueEditor>("Dialogue Editor");
        }

        // Helper to log a stack trace when destroying/removing nodes for diagnostics
        public static void LogNodeDestroyStack(UnityEngine.Object obj, string reason = "")
        {
            try
            {
                Debug.LogWarning($"About to destroy/remove object: {obj} Reason: {reason}\nStackTrace:\n" + Environment.StackTrace);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to log stack trace for node destroy: " + ex.Message);
            }
        }

        // Schedule a delayed removal of a sub-asset to avoid deleting objects while Unity's IMGUI/ObjectSelector
        // or other UI systems are processing events (which can cause MissingReferenceExceptions).
        private static HashSet<string> scheduledNodeRemovals = new HashSet<string>();

        public static void ScheduleDelayedNodeRemoval(DialogueNode node, Dialogue parentDialogue, string reason = "")
        {
            if (node == null || parentDialogue == null) return;
            if (string.IsNullOrEmpty(node.nodeId)) return;

            // Prevent double-scheduling
            if (scheduledNodeRemovals.Contains(node.nodeId))
            {
                Debug.LogWarning($"Node {node.name} ({node.nodeId}) already scheduled for removal; skipping duplicate.");
                return;
            }
            scheduledNodeRemovals.Add(node.nodeId);

            LogNodeDestroyStack(node, reason + " (scheduled)");

            // Use delayCall to postpone destructive operations until it's safe
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // If the node or parent were destroyed in the meantime, bail out
                    if (node == null)
                    {
                        scheduledNodeRemovals.Remove(node?.nodeId);
                        return;
                    }

                    // Remove data references from parent dialogue
                    if (parentDialogue != null && parentDialogue.nodes.Contains(node))
                    {
                        parentDialogue.nodes.Remove(node);
                        parentDialogue.nodePositions.RemoveAll(np => np.nodeId == node.nodeId);
                        EditorUtility.SetDirty(parentDialogue);
                    }

                    // Clear Inspector selection if it's referencing the node to avoid MR exceptions
                    if (Selection.activeObject == node && parentDialogue != null)
                    {
                        Selection.activeObject = parentDialogue;
                    }

                    // Register undo for the dialogue (so user can undo node removal)
                    if (parentDialogue != null)
                        Undo.RegisterCompleteObjectUndo(parentDialogue, "Delete Node");

                    // Final log for the actual destroy moment
                    LogNodeDestroyStack(node, reason + " (delayed destroy)");

                    // Use Undo.DestroyObjectImmediate so the deletion is undoable
                    try
                    {
                        Undo.DestroyObjectImmediate(node);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Error destroying node with Undo: " + ex.Message + "\n" + ex.StackTrace);
                        try
                        {
                            UnityEngine.Object.DestroyImmediate(node, true);
                        }
                        catch (Exception ex2)
                        {
                            Debug.LogError("Fallback destroy also failed: " + ex2.Message + "\n" + ex2.StackTrace);
                        }
                    }

                    AssetDatabase.SaveAssets();
                }
                catch (Exception ex)
                {
                    Debug.LogError("Error during delayed node removal: " + ex.Message + "\n" + ex.StackTrace);
                }
                finally
                {
                    scheduledNodeRemovals.Remove(node.nodeId);
                }
            };
        }

        private void OnEnable()
        {
            // ConstructGraphView();
            // AddToolbar();
            rootVisualElement.Clear();

            // Fallback IMGUI toolbar (ensures toolbar is visible even if UIElements toolbar is hidden)
            var imguiToolbar = new IMGUIContainer(() =>
            {
                GUILayout.BeginHorizontal(EditorStyles.toolbar);
                if (GUILayout.Button("Preview", EditorStyles.toolbarButton)) PreviewDialogue();
                // Rebuild Graph button removed per user request

                GUILayout.Space(8);
                // Dialogue picker so users can select an asset directly from the toolbar
                GUILayout.Label("Dialogue:", EditorStyles.label);
                var pickedDialogue = EditorGUILayout.ObjectField(selectedDialogue, typeof(Dialogue), false, GUILayout.Width(180)) as Dialogue;
                if (pickedDialogue != selectedDialogue)
                {
                    // Save current dialogue and clear the existing graph to avoid lingering references
                    try
                    {
                        SaveDialogue();
                    }
                    catch (Exception) { }

                    if (graphView != null)
                    {
                        var elems = graphView.graphElements.ToList();
                        if (elems.Count > 0)
                        {
                            // Suppress deletion callbacks so nodes are not removed from the underlying asset
                            suppressGraphViewDeletion = true;
                            graphView.DeleteElements(elems);
                            suppressGraphViewDeletion = false;
                        }
                    }

                    selectedDialogue = pickedDialogue;
                    // Rebuild graph when switching dialogue so UI reflects the selected asset
                    if (selectedDialogue != null)
                    {
                        RebuildGraph();
                    }
                }

                GUILayout.Space(6);
                // Diagnostic dry-run toggle (IMGUI fallback)
                var newDiag = GUILayout.Toggle(DialogueEditor.diagnosticsDryRun, "Diagnostics (dry-run)", GUILayout.Width(160));
                if (newDiag != DialogueEditor.diagnosticsDryRun)
                {
                    DialogueEditor.diagnosticsDryRun = newDiag;
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            });
            imguiToolbar.style.height = 26;
            imguiToolbar.style.flexShrink = 0;
            rootVisualElement.Add(imguiToolbar);

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.flexGrow = 1;


            graphView = new DialogueGraphView(this)
            {
                name = "Dialogue Graph"
            };
            graphView.StretchToParentSize();
            graphView.style.flexGrow = 1;
            container.Add(graphView);

            rootVisualElement.Add(container);

            graphView.nodeCreationRequest = context => CreateNode(context.screenMousePosition);
            graphView.graphViewChanged = OnGraphChanged;
            graphView.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            // If a dialogue was loaded before, reload it (persists across enables)
            if (selectedDialogue != null)
            {
                RebuildGraph();
            }

            // Add UIElements toolbar after adding the graph view so the toolbar renders on top
            AddToolbar(container); // Ensure called—toolbar fixed to be top-most
        }

        private void OnDisable()
        {
            rootVisualElement.Clear();
        }

        private void ConstructGraphView()
        {
            graphView = new DialogueGraphView(this)
            {
                name = "Dialogue Graph",
                style = { flexGrow = 1 }
            };
            graphView.StretchToParentSize();
            rootVisualElement.Add(graphView);

            graphView.nodeCreationRequest = context => CreateNode(context.screenMousePosition);
            graphView.graphViewChanged = OnGraphChanged;

            // Enable right-click context menu for creating nodes
            graphView.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
        }

        private void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Store the mouse position for accurate node placement
            lastMousePosition = evt.mousePosition;
            evt.menu.AppendAction("Create Node", a => CreateNode(lastMousePosition));
        }

        private void AddToolbar(VisualElement parent)
        {
            var toolbar = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 5,
                    paddingBottom = 5,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                    height = 30, // Explicit height to force visibility
                    borderBottomWidth = 1, // Visual separator
                    borderBottomColor = Color.gray
                }
            };

            var previewButton = new Button(PreviewDialogue) { text = "Preview" }; // Runtime test

            // Add with flex for spacing
            previewButton.style.flexGrow = 1;

            toolbar.Add(previewButton);

            // Create a stacked toolbar area: top row for buttons, bottom row for defaults/foldout
            var toolbarStack = new VisualElement { style = { flexDirection = FlexDirection.Column } };

            // Top row: main buttons
            var topRow = toolbar;
            topRow.style.minHeight = 30;
            topRow.style.flexShrink = 0;

            // UIElements diagnostic toggle (keeps UIElements and IMGUI in sync)
            var diagToggle = new Toggle("Diagnostics (dry-run)") { value = DialogueEditor.diagnosticsDryRun };
            diagToggle.RegisterValueChangedCallback(evt => { DialogueEditor.diagnosticsDryRun = evt.newValue; });
            // Place toggle at the end of top row
            topRow.Add(diagToggle);

            // Bottom row: defaults area with explicit height so it's visible
            var bottomRow = new VisualElement { style = { flexDirection = FlexDirection.Row, minHeight = 72, paddingLeft = 6, paddingTop = 4, paddingBottom = 4, backgroundColor = new Color(0.15f, 0.15f, 0.15f) } };
            bottomRow.style.flexShrink = 0;

            toolbarStack.Add(topRow);
            toolbarStack.Add(bottomRow);
            // Ensure toolbar draws on top of GraphView
            toolbarStack.BringToFront();
            parent.Add(toolbarStack);

            // Add defaults UI directly under toolbar (shows/edits selectedDialogue defaults)
            var defaultsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 4, paddingBottom = 4 } };

            var speakerField = new ObjectField("Default Speaker") { objectType = typeof(Character) };
            var listenerField = new ObjectField("Default Listener") { objectType = typeof(Character) };
            var applyButton = new Button(() =>
            {
                // Apply defaults to all existing nodes (optional convenience)
                if (selectedDialogue == null) return;
                Undo.RegisterCompleteObjectUndo(selectedDialogue, "Apply Defaults to Nodes");
                foreach (var n in selectedDialogue.nodes)
                {
                    if (speakerField.value != null) n.speakerCharacter = speakerField.value as Character;
                    if (listenerField.value != null) n.listenerCharacter = listenerField.value as Character;
                    EditorUtility.SetDirty(n);
                }
                EditorUtility.SetDirty(selectedDialogue);
                AssetDatabase.SaveAssets();
                // Refresh visible node views so editor shows updated speaker/listener without reloading
                if (graphView != null)
                {
                    foreach (var nv in graphView.nodes.OfType<DialogueNodeView>())
                    {
                        if (nv.dialogueNode != null)
                        {
                            nv.RefreshFromModel();
                        }
                    }
                }
            }) { text = "Apply To Existing Nodes" };

            speakerField.RegisterValueChangedCallback(evt =>
            {
                if (selectedDialogue == null) return;
                Undo.RecordObject(selectedDialogue, "Set Default Speaker");
                selectedDialogue.defaultSpeaker = evt.newValue as Character;
                EditorUtility.SetDirty(selectedDialogue);
            });

            listenerField.RegisterValueChangedCallback(evt =>
            {
                if (selectedDialogue == null) return;
                Undo.RecordObject(selectedDialogue, "Set Default Listener");
                selectedDialogue.defaultListener = evt.newValue as Character;
                EditorUtility.SetDirty(selectedDialogue);
            });

            // Keep fields in sync when a Dialogue asset is selected/loaded
            void RefreshDefaultsFields()
            {
                speakerField.value = selectedDialogue != null ? selectedDialogue.defaultSpeaker : null;
                listenerField.value = selectedDialogue != null ? selectedDialogue.defaultListener : null;
            }

            // Create a foldout/dropdown for defaults
            var defaultsFold = new Foldout { text = "Default Characters", value = false };
            // Style to ensure visibility
            defaultsFold.style.flexShrink = 0;
            defaultsFold.style.paddingLeft = 4;
            defaultsFold.style.marginTop = 4;

            speakerField.style.width = 300;
            listenerField.style.width = 300;

            defaultsFold.Add(speakerField);
            defaultsFold.Add(listenerField);

            int expandedHeight = 72;
            int collapsedHeight = 28;

            bottomRow.style.minHeight = defaultsFold.value ? expandedHeight : collapsedHeight;

            defaultsFold.RegisterValueChangedCallback(evt =>
            {
                bottomRow.style.minHeight = evt.newValue ? expandedHeight : collapsedHeight;
            });

            var applyRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var applyToggle = new Toggle("Only apply to missing") { value = applyOnlyIfMissing };
            applyToggle.RegisterValueChangedCallback(evt => { applyOnlyIfMissing = evt.newValue; });
            applyRow.Add(applyToggle);

            applyRow.Add(applyButton);
            defaultsFold.Add(applyRow);
            // Put the foldout into the bottomRow to ensure it gets visible space
            bottomRow.Add(defaultsFold);

            // Call once to initialize (if a dialogue is already selected)
            RefreshDefaultsFields();
        }

        public DialogueNodeView CreateNode(Vector2 screenPosition)
        {
            if (selectedDialogue == null)
            {
                Debug.LogError("Please load a Dialogue asset first before creating nodes.");
                return null;
            }

            // Convert screen position to graph view's local coordinates
            var localPosition = graphView.contentViewContainer.WorldToLocal(screenPosition);

            var node = ScriptableObject.CreateInstance<DialogueNode>();
            node.nodeId = Guid.NewGuid().ToString();
            node.name = $"Node_{selectedDialogue.nodes.Count}";
            // Initialize node speaker/listener from dialogue defaults (designer convenience)
            if (selectedDialogue.defaultSpeaker != null)
            {
                node.speakerCharacter = selectedDialogue.defaultSpeaker;
                node.speakerName = selectedDialogue.defaultSpeaker.npcName;
                if (string.IsNullOrEmpty(node.speakerExpression)) node.speakerExpression = "Default";
            }

            if (selectedDialogue.defaultListener != null)
            {
                node.listenerCharacter = selectedDialogue.defaultListener;
            }

            AssetDatabase.AddObjectToAsset(node, selectedDialogue);
            selectedDialogue.nodes.Add(node);
            selectedDialogue.SetNodePosition(node.nodeId, new Rect(localPosition, new Vector2(250, 300)));
            EditorUtility.SetDirty(selectedDialogue);
            AssetDatabase.SaveAssets();

            var nodeView = AddNodeToGraph(node, localPosition);
            return nodeView;
        }

        private DialogueNodeView AddNodeToGraph(DialogueNode dialogueNode, Vector2 position)
        {
            // Load saved position if available
            var savedPos = selectedDialogue.GetNodePosition(dialogueNode.nodeId);
            var finalPos = savedPos != Vector2.zero ? savedPos : position;

            if (dialogueNode == null)
            {
                Debug.LogWarning("Attempted to add a null/destroyed DialogueNode to graph; skipping.");
                return null;
            }

            var nodeView = new DialogueNodeView(dialogueNode, selectedDialogue)
            {
                title = dialogueNode.speakerName != "" ? dialogueNode.speakerName : "Node"
            };
            nodeView.SetPosition(new Rect(finalPos, new Vector2(250, 300)));
            graphView.AddElement(nodeView);

            // Refresh choices display
            nodeView.RefreshChoices();
            nodeView.RefreshBranches();

            return nodeView;
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var outputNodeView = edge.output.node as DialogueNodeView;
                    var inputNodeView = edge.input.node as DialogueNodeView;
                    if (outputNodeView == null || inputNodeView == null) continue;
                    
                    int index = outputNodeView.GetOutputPortIndex(edge.output);
                    if (index >= 0)
                    {
                        var choiceCount = outputNodeView.dialogueNode.choices.Count;
                        // Check if this is a choice or branch port (assume choices first, branches after in outputContainer)
                        if (index < choiceCount)
                        {
                            // Choice connection
                            outputNodeView.dialogueNode.choices[index].targetNode = inputNodeView.dialogueNode;
                        }
                        else
                        {
                            // Branch connection (adjust index)
                            int branchIndex = index - choiceCount; // If branches follow choices in container
                            if (branchIndex < outputNodeView.dialogueNode.conditionalBranches.Count)
                            {
                                outputNodeView.dialogueNode.conditionalBranches[branchIndex].targetNode = inputNodeView.dialogueNode;
                            }
                        }
                    }
                    else
                    {
                        // Linear nextNode
                        outputNodeView.dialogueNode.nextNode = inputNodeView.dialogueNode;
                    }
                    EditorUtility.SetDirty(outputNodeView.dialogueNode);
                    EditorUtility.SetDirty(selectedDialogue);
                    AssetDatabase.SaveAssets();
                }
            }

            // Handle deletions (optional but good for cleanup)
            if (!suppressGraphViewDeletion && change.elementsToRemove != null)
            {
                foreach (var elem in change.elementsToRemove)
                {
                    if (elem is Edge edge)
                    {
                        // Disconnect logic (set targetNode null if needed)
                        var outputView = edge.output.node as DialogueNodeView;
                        if (outputView != null)
                        {
                            int index = outputView.GetOutputPortIndex(edge.output);
                            if (index >= 0)
                            {
                                var choiceCount = outputView.dialogueNode.choices.Count;
                                if (index < choiceCount)
                                {
                                    outputView.dialogueNode.choices[index].targetNode = null;
                                }
                                else
                                {
                                    int branchIndex = index - choiceCount;
                                    if (branchIndex < outputView.dialogueNode.conditionalBranches.Count)
                                    {
                                        outputView.dialogueNode.conditionalBranches[branchIndex].targetNode = null;
                                    }
                                }
                                EditorUtility.SetDirty(outputView.dialogueNode);
                            }
                        }
                    }
                    else if (elem is DialogueNodeView nodeView)
                    {
                        // Clean up node (from your code)
                        selectedDialogue.nodes.Remove(nodeView.dialogueNode);
                        selectedDialogue.nodePositions.RemoveAll(np => np.nodeId == nodeView.dialogueNode.nodeId);
                        EditorUtility.SetDirty(selectedDialogue);
                        if (nodeView.dialogueNode != null)
                        {
                            // Schedule removal to avoid deleting during UI event processing
                            DialogueEditor.ScheduleDelayedNodeRemoval(nodeView.dialogueNode, selectedDialogue, "DeleteNode via GraphView deleteSelection");
                        }
                    }
                }
            }

            // Handle node movement to save positions
            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is DialogueNodeView nodeView)
                    {
                        selectedDialogue.SetNodePosition(nodeView.dialogueNode.nodeId, nodeView.GetPosition());
                        EditorUtility.SetDirty(selectedDialogue);
                    }
                }
            }

            return change;
        }

        private void LoadDialogue()
        {
            var path = EditorUtility.OpenFilePanel("Load Dialogue", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;

            path = path.Replace(Application.dataPath, "Assets");
            selectedDialogue = AssetDatabase.LoadAssetAtPath<Dialogue>(path);
            if (selectedDialogue == null)
            {
                Debug.LogError("Failed to load Dialogue SO—check path or type.");
                return;
            }

            RebuildGraph(); // Clear and reload nodes/edges
            Debug.Log($"Loaded Dialogue: {selectedDialogue.name}");
        }
        
        // public void LoadDialogueAsset(Dialogue dialogue) // For double-click
        // {
        //     selectedDialogue = dialogue;
        //     RebuildGraph();
        //     Debug.Log($"Auto-opened: {dialogue.name}");
        // }



        private void RebuildGraph()
        {
            graphView.DeleteElements(graphView.graphElements.ToList()); // Clear all

            if (selectedDialogue == null) return;

            // If this dialogue has no nodes (newly created asset), create a default start node
            if (selectedDialogue.nodes == null || selectedDialogue.nodes.Count == 0)
            {
                var newNode = ScriptableObject.CreateInstance<DialogueNode>();
                newNode.nodeId = Guid.NewGuid().ToString();
                newNode.name = "Node_0";

                // Initialize from dialogue defaults if available
                if (selectedDialogue.defaultSpeaker != null)
                {
                    newNode.speakerCharacter = selectedDialogue.defaultSpeaker;
                    newNode.speakerName = selectedDialogue.defaultSpeaker.npcName;
                    if (string.IsNullOrEmpty(newNode.speakerExpression)) newNode.speakerExpression = "Default";
                }
                if (selectedDialogue.defaultListener != null)
                {
                    newNode.listenerCharacter = selectedDialogue.defaultListener;
                }

                AssetDatabase.AddObjectToAsset(newNode, selectedDialogue);
                selectedDialogue.nodes.Add(newNode);
                selectedDialogue.SetNodePosition(newNode.nodeId, new Rect(0, 0, 250, 300));
                selectedDialogue.startNode = newNode;
                EditorUtility.SetDirty(selectedDialogue);
                AssetDatabase.SaveAssets();
            }

            // Defensive cleanup: remove any null/destroyed nodes from the SO to avoid exceptions
            var removed = selectedDialogue.nodes.Where(n => n == null).ToList();
            if (removed.Count > 0)
            {
                foreach (var r in removed)
                {
                    Debug.LogWarning("Removing null/destroyed DialogueNode from Dialogue asset during RebuildGraph.");
                    selectedDialogue.nodes.Remove(r);
                }
                EditorUtility.SetDirty(selectedDialogue);
                AssetDatabase.SaveAssets();
            }

            // Add nodes
            var nodeViews = new Dictionary<string, DialogueNodeView>();
            foreach (var node in selectedDialogue.nodes)
            {
                var nodeView = AddNodeToGraph(node, selectedDialogue.GetNodePosition(node.nodeId));
                nodeViews[node.nodeId] = nodeView;
            }

            // Reconnect edges
            foreach (var node in selectedDialogue.nodes)
            {
                var nodeView = nodeViews[node.nodeId];
                int portIndex = 0; // 0 for nextNode if present

                if (node.nextNode != null && nodeViews.TryGetValue(node.nextNode.nodeId, out var nextView))
                {
                    ConnectPorts(nodeView.outputContainer[portIndex] as Port, nextView.inputContainer[0] as Port);
                }
                portIndex++;

                for (int i = 0; i < node.choices.Count; i++)
                {
                    var target = node.choices[i].targetNode;
                    if (target != null && nodeViews.TryGetValue(target.nodeId, out var targetView))
                    {
                        ConnectPorts(nodeView.outputContainer[portIndex] as Port, targetView.inputContainer[0] as Port);
                    }
                    portIndex++;
                }

                for (int i = 0; i < node.conditionalBranches.Count; i++)
                {
                    var target = node.conditionalBranches[i].targetNode;
                    if (target != null && nodeViews.TryGetValue(target.nodeId, out var targetView))
                    {
                        ConnectPorts(nodeView.outputContainer[portIndex] as Port, targetView.inputContainer[0] as Port);
                    }
                    portIndex++;
                }
            }

            // After rebuilding, frame the start node (or the first node found)
            FrameStartNode();
        }

        // New: Frame (focus) the start node (or first node when multiple/no explicit start)
        public void FrameStartNode()
        {
            if (selectedDialogue == null || graphView == null) return;
            DialogueNode target = selectedDialogue.startNode ?? selectedDialogue.nodes.FirstOrDefault();
            if (target == null) return;

            var nodeView = graphView.nodes.OfType<DialogueNodeView>().FirstOrDefault(nv => nv.dialogueNode == target);
            if (nodeView != null)
            {
                // Try to use GraphView.FrameElements via reflection when available to center on the specific node.
                try
                {
                    var gvType = typeof(GraphView);
                    var mi = gvType.GetMethod("FrameElements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (mi != null)
                    {
                        var listType = typeof(List<GraphElement>);
                        var elements = Activator.CreateInstance(listType) as System.Collections.IList;
                        elements.Add(nodeView);
                        mi.Invoke(graphView, new object[] { elements, true });
                        return;
                    }
                }
                catch (Exception)
                {
                    // ignore and fallback
                }

                // Fallback: frame all nodes (less precise but always available)
                graphView.FrameAll();
            }
            else
            {
                // If we couldn't find the view, fallback to framing all nodes
                graphView.FrameAll();
            }
        }

        private void ConnectPorts(Port output, Port input)
        {
            if (output == null || input == null) return;
            var edge = output.ConnectTo(input);
            graphView.AddElement(edge);
        }

        // New: Auto-center and frame all nodes after load
        private void FrameNodes()
        {
            if (selectedDialogue?.nodes == null || selectedDialogue.nodes.Count == 0) return;

            var nodes = graphView.nodes.ToList();
            if (nodes.Count == 0) return;

            // Use FrameAll to center on all nodes
            graphView.FrameAll();
        }

        private void SaveDialogue()
        {
            if (selectedDialogue == null) return;

            Undo.RegisterCompleteObjectUndo(selectedDialogue, "Save Dialogue"); // Safety net
            AssetDatabase.SaveAssetIfDirty(selectedDialogue);
            foreach (var node in selectedDialogue.nodes)
            {
                Undo.RegisterCompleteObjectUndo(node, "Save Node");
                AssetDatabase.SaveAssetIfDirty(node);
            }
            Debug.Log("Saved Dialogue and nodes.");
        }

        private void PreviewDialogue()
        {
            if (selectedDialogue?.startNode == null)
            {
                Debug.LogWarning("No start node set for preview.");
                return;
            }

            Debug.Log("Dialogue Preview:");
            var node = selectedDialogue.startNode;
            int depth = 0;
            while (node != null && depth < 100) // Prevent infinite loops
            {
                Debug.Log($"{node.speakerName}: {node.dialogueText}");
                if (node.choices.Count > 0)
                {
                    Debug.Log("Choices: " + string.Join(", ", node.choices.Select(c => c.choiceText)));
                }
                node = node.nextNode; // Simplified: follows linear path
                depth++;
            }
        }

        [OnOpenAsset(1)]
        public static bool OnOpenDialogueAsset(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj is Dialogue dialogueAsset)
            {
                // Save the current selectedDialogue if one is present.
                var window = EditorWindow.GetWindow<DialogueEditor>();

                if (window.selectedDialogue != null)
                {
                    window.SaveDialogue();
                    Debug.Log("Saving " + window.selectedDialogue.name);
                }

                /*
                    Attempt to solve the Dialogue breaking bug where Dialogue files break
                    when opening another Dialogue object when  one is currently open
                */
                var openWindows = Resources.FindObjectsOfTypeAll<DialogueEditor>();
                foreach (var win in openWindows)
                {
                    win.Close();
                }

                DialogueEditor.Open();
                window = EditorWindow.GetWindow<DialogueEditor>();
                window.selectedDialogue = dialogueAsset;
                // Defer RebuildGraph until the window is fully initialized
                EditorApplication.delayCall += () =>
                {
                    if (window != null)
                    {
                        window.RebuildGraph();
                    }
                };
                Debug.Log("Opening " + window.selectedDialogue.name);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Custom GraphView for dialogue nodes.
    /// Handles node rendering, connections, and custom deletion behavior.
    /// </summary>
    public class DialogueGraphView : GraphView
    {
        public readonly DialogueEditor editor;

        public DialogueGraphView(DialogueEditor editor)
        {
            this.editor = editor;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // Add grid background with better visibility
            var grid = new GridBackground() {
                name = "GridBackground"
            };
            grid.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1); // Dark background
            // Make grid lines more visible
            var gridStyle = grid.style;
            gridStyle.unityBackgroundImageTintColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Insert(0, grid);
            grid.StretchToParentSize();

            

            // Set up edge connector for creating nodes on drop
            var edgeConnector = new EdgeConnector<Edge>(new CustomEdgeConnectorListener(this, editor));
            this.AddManipulator(edgeConnector);

            // Hook into deletion to handle node removal from ScriptableObject
            deleteSelection = (operation, askUser) =>
            {
                // Filter nodes to delete, excluding start node
                var nodesToDelete = selection.OfType<DialogueNodeView>()
                    .Where(nodeView => editor.selectedDialogue != null && nodeView.dialogueNode != editor.selectedDialogue.startNode)
                    .ToList();

                // Delete nodes using DialogueNodeView.DeleteNode
                foreach (var nodeView in nodesToDelete)
                {
                    nodeView.DeleteNode();
                }

                // Delete edges
                var edgesToDelete = selection.OfType<Edge>().ToList();
                foreach (var edge in edgesToDelete)
                {
                    if (edge.output != null && edge.input != null)
                    {
                        var outputNode = edge.output.node as DialogueNodeView;
                        var inputNode = edge.input.node as DialogueNodeView;
                        if (outputNode != null && inputNode != null)
                        {
                            int choiceIndex = outputNode.GetOutputPortIndex(edge.output);
                            if (choiceIndex >= 0)
                            {
                                outputNode.dialogueNode.choices[choiceIndex].targetNode = null;
                            }
                            else
                            {
                                outputNode.dialogueNode.nextNode = null;
                            }
                            EditorUtility.SetDirty(outputNode.dialogueNode);
                        }
                    }
                    RemoveElement(edge);
                }
            };
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            foreach (var port in ports.ToList())
            {
                if (startPort != port && startPort.node != port.node &&
                    startPort.direction != port.direction)
                {
                    compatiblePorts.Add(port);
                }
            }
            return compatiblePorts;
        }

        public DialogueNodeView CreateNodeAtPosition(Vector2 localPosition)
        {
            // Convert to world position for the CreateNode method
            var worldPosition = contentViewContainer.LocalToWorld(localPosition);
            return editor.CreateNode(worldPosition);
        }
    }

    /// <summary>
    /// Custom listener for edge connections, including creating new nodes on drop outside ports.
    /// </summary>
    public class CustomEdgeConnectorListener : IEdgeConnectorListener
    {
        private readonly GraphView graphView;
        private readonly DialogueEditor editor;

        public CustomEdgeConnectorListener(GraphView graphView, DialogueEditor editor)
        {
            this.graphView = graphView;
            this.editor = editor;
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            var outputNode = edge.output.node as DialogueNodeView;
            if (outputNode == null) return;

            // Convert drop position to graph view's local coordinates
            var localPosition = graphView.contentViewContainer.WorldToLocal(position);

            // Create a new node at the drop position
            var newNodeView = editor.CreateNode(localPosition);
            if (newNodeView != null)
            {
                // Connect the edge to the new node
                var inputPort = newNodeView.inputContainer[0].Q<Port>();
                edge.input = inputPort;
                inputPort.Connect(edge);
                graphView.AddElement(edge);

                // Update the dialogue node connections
                int choiceIndex = outputNode.GetOutputPortIndex(edge.output);
                if (choiceIndex >= 0)
                {
                    outputNode.dialogueNode.choices[choiceIndex].targetNode = newNodeView.dialogueNode;
                }
                else
                {
                    outputNode.dialogueNode.nextNode = newNodeView.dialogueNode;
                }
                EditorUtility.SetDirty(editor.selectedDialogue);
            }
            else
            {
                edge.input = null;
                edge.output = null;
                edge.RemoveFromHierarchy();
            }
        }

        public void OnDrop(GraphView graphView, Edge edge)
        {
            // Standard connection handling
        }
    }

    /// <summary>
    /// Visual representation of a DialogueNode in the GraphView.
    /// </summary>
    public class DialogueNodeView : Node
    {
        public DialogueNode dialogueNode;
        private readonly Dialogue selectedDialogue;
        private readonly List<VisualElement> choiceElements = new List<VisualElement>();
        private VisualElement branchContainer;
        private List<Port> branchPorts = new List<Port>();
        private List<VisualElement> branchElements = new List<VisualElement>();
        PopupField<string> expressionDropdown = null;
        private PopupField<string> listenerExpressionDropdown = null;
        private TextField speakerField;
        private ObjectField charField;
        private ObjectField listenerField;
        private Toggle showListenerToggle;
    // NEW: single reusable IsSpeaker toggle instance — prevents duplicates on repeated RefreshCharFields calls
    private Toggle isSpeakerToggle;

        public DialogueNodeView(DialogueNode node, Dialogue dialogue)
        {
            dialogueNode = node;
            selectedDialogue = dialogue;

            // Input port
            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(DialogueNode));
            inputPort.portName = "Input";
            inputContainer.Add(inputPort);

            // Output port for linear progression
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueNode));
            outputPort.portName = "Next";
            outputContainer.Add(outputPort);

            // Register for position changes to save
            this.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            // Add context menu for node deletion
            this.AddManipulator(new ContextualMenuManipulator(BuildNodeContextMenu));

            // Node fields (basic; detailed editing via Inspector)
            speakerField = new TextField("Speaker Name") { value = dialogueNode.speakerName };
            speakerField.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.speakerName = evt.newValue;
                title = string.IsNullOrEmpty(evt.newValue) ? "Node" : evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
            });
            mainContainer.Add(speakerField);

            charField = new ObjectField("Speaker") { objectType = typeof(Character), value = dialogueNode.speakerCharacter };
            charField.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.speakerCharacter = evt.newValue as Character;
                EditorUtility.SetDirty(dialogueNode);
                RefreshCharFields();
            });
            mainContainer.Add(charField);

            /// ----- ADD LISTENER CHARACTER ------ ///
            showListenerToggle = new Toggle("Show Listener") { value = dialogueNode.listenerCharacter != null };
            showListenerToggle.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    dialogueNode.listenerCharacter = null;
                    dialogueNode.listenerExpression = null;
                }
                EditorUtility.SetDirty(dialogueNode);
                RefreshCharFields();
            });
            mainContainer.Add(showListenerToggle);

            listenerField = new ObjectField("Listener") { objectType = typeof(Character), value = dialogueNode.listenerCharacter };
            listenerField.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.listenerCharacter = evt.newValue as Character;
                EditorUtility.SetDirty(dialogueNode);
                RefreshCharFields();
            });
            //mainContainer.Add(listenerField);

            
            /// ----- END OF LISTENER CHARACTER ----- ///
            

            var textField = new TextField("Dialogue Text") { value = dialogueNode.dialogueText, multiline = true };
            textField.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.dialogueText = evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
            });
            mainContainer.Add(textField);

            // Start node toggle
            var startToggle = new Toggle("Start Node") { value = dialogue.startNode == dialogueNode };
            startToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    dialogue.startNode = dialogueNode;
                }
                else if (dialogue.startNode == dialogueNode)
                {
                    dialogue.startNode = null;
                }
                EditorUtility.SetDirty(dialogue);
            });
            mainContainer.Add(startToggle);

            var addChoiceButton = new Button(() => AddChoice()) { text = "Add Choice" };
            mainContainer.Add(addChoiceButton);

            branchContainer = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            extensionContainer.Add(branchContainer); // Or inputContainer for top placement

            var addBranchButton = new Button(AddBranch) { text = "Add Conditional Branch" };
            extensionContainer.Add(addBranchButton); // Place below choices

            RefreshBranches(); // Initial call
            
        }

        // Refresh UI elements to reflect the underlying DialogueNode model
        public void RefreshFromModel()
        {
            if (dialogueNode == null) return;

            // Update title and text fields
            title = !string.IsNullOrEmpty(dialogueNode.speakerName) ? dialogueNode.speakerName : "Node";

            // Update character object fields
            if (charField != null) charField.value = dialogueNode.speakerCharacter;
            if (listenerField != null) listenerField.value = dialogueNode.listenerCharacter;

            // Refresh dynamic parts
            RefreshChoices();
            RefreshBranches();
            RefreshCharFields();
        }

        void RefreshCharFields()
        {
            // if (expressionDropdown != null)
            //     mainContainer.Remove(expressionDropdown);

            // if (dialogueNode.character != null)
            // {
            //     var expressions = dialogueNode.character.expressions.ConvertAll(e => e.expressionName);
            //     if (!expressions.Contains("Default")) expressions.Insert(0, "Default");

            //     // Ensure the current value is valid
            //     string currentValue = dialogueNode.charExpression;
            //     if (string.IsNullOrEmpty(currentValue) || !expressions.Contains(currentValue))
            //     {
            //         currentValue = "Default";
            //         dialogueNode.charExpression = currentValue;
            //     }

            //     expressionDropdown = new PopupField<string>("Expression", expressions, currentValue);
            //     expressionDropdown.RegisterValueChangedCallback(e =>
            //     {
            //         dialogueNode.charExpression = e.newValue;
            //         EditorUtility.SetDirty(dialogueNode);
            //     });
            //     mainContainer.Add(expressionDropdown);
            // }
            // Remove old dropdown if present
            if (expressionDropdown != null)
            {
                mainContainer.Remove(expressionDropdown);
                expressionDropdown = null;
            }

            // Remove listener UI if present
            if (listenerField != null && mainContainer.Contains(listenerField))
                mainContainer.Remove(listenerField);
            if (listenerExpressionDropdown != null && mainContainer.Contains(listenerExpressionDropdown))
                mainContainer.Remove(listenerExpressionDropdown);

            if (dialogueNode.speakerCharacter != null)
            {
                // Overwrite speaker name with character SO name and disable editing
                speakerField.value = dialogueNode.speakerCharacter.npcName;
                speakerField.SetEnabled(false);

                dialogueNode.speakerName = dialogueNode.speakerCharacter.npcName;
                EditorUtility.SetDirty(dialogueNode);

                // Build expression list
                var expressions = dialogueNode.speakerCharacter.expressions.ConvertAll(e => e.expressionName);
                if (!expressions.Contains("Default")) expressions.Insert(0, "Default");

                // Ensure current value is valid
                string currentValue = dialogueNode.speakerExpression;
                if (string.IsNullOrEmpty(currentValue) || !expressions.Contains(currentValue))
                {
                    currentValue = "Default";
                    dialogueNode.speakerExpression = currentValue;
                }

                expressionDropdown = new PopupField<string>("Expression", expressions, currentValue);
                expressionDropdown.RegisterValueChangedCallback(e =>
                {
                    dialogueNode.speakerExpression = e.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                });

                // Insert directly after the character field
                mainContainer.Insert(mainContainer.IndexOf(charField) + 1, expressionDropdown);
            }
            else
            {
                // No character: enable speaker field for manual entry
                speakerField.SetEnabled(true);
            }

            // Expression dropdown for listener (similar to speaker)
            if (showListenerToggle.value)
            {
                if (!mainContainer.Contains(listenerField))
                    mainContainer.Add(listenerField);

                if (dialogueNode.listenerCharacter != null)
                {
                    var expressions = dialogueNode.listenerCharacter.expressions.ConvertAll(e => e.expressionName);
                    if (!expressions.Contains("Default")) expressions.Insert(0, "Default");
                    string currentValue = dialogueNode.listenerExpression;
                    if (string.IsNullOrEmpty(currentValue) || !expressions.Contains(currentValue))
                    {
                        currentValue = "Default";
                        dialogueNode.listenerExpression = currentValue;
                    }
                    listenerExpressionDropdown = new PopupField<string>("Listener Expression", expressions, currentValue);
                    listenerExpressionDropdown.RegisterValueChangedCallback(e =>
                    {
                        dialogueNode.listenerExpression = e.newValue;
                        EditorUtility.SetDirty(dialogueNode);
                    });
                    mainContainer.Add(listenerExpressionDropdown);
                }
            }

            // Toggle to turn on the listener as the speaker (reuse single toggle instance)
            if (isSpeakerToggle == null)
            {
                isSpeakerToggle = new Toggle("Is Speaker") { value = dialogueNode.listenerIsSpeaker };
                isSpeakerToggle.RegisterValueChangedCallback(e =>
                {
                    dialogueNode.listenerIsSpeaker = e.newValue;
                    EditorUtility.SetDirty(dialogueNode);

                    // If checked, override speaker name in editor UI
                    if (e.newValue && dialogueNode.listenerCharacter != null)
                    {
                        speakerField.value = dialogueNode.listenerCharacter.npcName;
                        speakerField.SetEnabled(false);
                        dialogueNode.speakerName = dialogueNode.listenerCharacter.npcName;
                    }
                    else
                    {
                        // Restore speaker field based on speakerCharacter
                        if (dialogueNode.speakerCharacter != null)
                        {
                            speakerField.value = dialogueNode.speakerCharacter.npcName;
                            speakerField.SetEnabled(false);
                            dialogueNode.speakerName = dialogueNode.speakerCharacter.npcName;
                        }
                        else
                        {
                            speakerField.SetEnabled(true);
                        }
                    }
                });
            }
            else
            {
                // keep toggle's current checked state in sync with the node
                isSpeakerToggle.value = dialogueNode.listenerIsSpeaker;
            }

            // Add the toggle to the UI (once)
            if (!mainContainer.Contains(isSpeakerToggle))
                mainContainer.Add(isSpeakerToggle);

            // Initial override if already checked
            if (dialogueNode.listenerIsSpeaker && dialogueNode.listenerCharacter != null)
            {
                speakerField.value = dialogueNode.listenerCharacter.npcName;
                speakerField.SetEnabled(false);
                dialogueNode.speakerName = dialogueNode.listenerCharacter.npcName;
            }
        }

        // Override to handle selection: Show this node's SO in Inspector
        public override void OnSelected()
        {
            base.OnSelected();
            // Select the underlying DialogueNode SO in Unity's Selection
            // This swaps the Inspector to show its properties (conditions, consequences, etc.)
            Selection.activeObject = dialogueNode;
            EditorGUIUtility.PingObject(dialogueNode); // Optional: Highlights in Project window
        }

        // Override to handle deselection: Fallback to Dialogue asset
        public override void OnUnselected()
        {
            base.OnUnselected();
            // Optional: Clear selection if deselected (or keep last node selected)
            if (Selection.activeObject == dialogueNode)
            {
                Selection.activeObject = selectedDialogue; // Fallback to the Dialogue asset
            }
        }

        private void BuildNodeContextMenu(ContextualMenuPopulateEvent evt)
        {
            // Add option to create connected node
            evt.menu.AppendAction("Create Connected Node", a => CreateConnectedNode());

            if (selectedDialogue.startNode != dialogueNode)
            {
                evt.menu.AppendAction("Delete Node", a => DeleteNode());
            }
            else
            {
                evt.menu.AppendAction("Delete Node", a => { }, DropdownMenuAction.Status.Disabled);
            }
        }

        private void CreateConnectedNode()
        {
            if (selectedDialogue == null) return;

            // Calculate position to the right of current node
            var currentPos = GetPosition();
            var newPosition = new Vector2(currentPos.x + currentPos.width + 50, currentPos.y);

            // Get the graph view reference
            var graphView = this.GetFirstAncestorOfType<DialogueGraphView>();
            if (graphView == null) return;

            // Convert to world position for the CreateNode method
            var worldPosition = graphView.contentViewContainer.LocalToWorld(newPosition);

            // Create the new node using the editor's method
            var editor = graphView.editor;
            var newNodeView = editor.CreateNode(worldPosition);

            if (newNodeView != null)
            {
                // Connect the current node to the new node
                dialogueNode.nextNode = newNodeView.dialogueNode;
                EditorUtility.SetDirty(dialogueNode);
                EditorUtility.SetDirty(selectedDialogue);

                // Create visual connection
                var outputPort = outputContainer[0].Q<Port>();
                var inputPort = newNodeView.inputContainer[0].Q<Port>();
                var edge = outputPort.ConnectTo(inputPort);
                graphView.AddElement(edge);
            }
        }

        public void DeleteNode()
        {
            if (selectedDialogue == null || dialogueNode == null) return;
            if (selectedDialogue.startNode == dialogueNode)
            {
                Debug.LogWarning("Cannot delete the start node.");
                return;
            }

            // Disconnect all input and output connections
            var inputPort = inputContainer.Q<Port>();
            if (inputPort != null)
            {
                var inputConnections = inputPort.connections.ToList();
                foreach (var edge in inputConnections)
                {
                    edge.input.Disconnect(edge);
                    edge.output.Disconnect(edge);
                    edge.RemoveFromHierarchy();
                }
            }

            foreach (var port in outputContainer.Children().OfType<Port>())
            {
                var outputConnections = port.connections.ToList();
                foreach (var edge in outputConnections)
                {
                    edge.input.Disconnect(edge);
                    edge.output.Disconnect(edge);
                    edge.RemoveFromHierarchy();
                }
            }

            // Clear references to this node in other nodes
            foreach (var otherNode in selectedDialogue.nodes)
            {
                if (otherNode == dialogueNode) continue;
                if (otherNode.nextNode == dialogueNode)
                {
                    otherNode.nextNode = null;
                    EditorUtility.SetDirty(otherNode);
                }
                for (int i = 0; i < otherNode.choices.Count; i++)
                {
                    if (otherNode.choices[i].targetNode == dialogueNode)
                    {
                        otherNode.choices[i].targetNode = null;
                        EditorUtility.SetDirty(otherNode);
                    }
                }
            }

            // Remove node position
            selectedDialogue.nodePositions.RemoveAll(np => np.nodeId == dialogueNode.nodeId);

            // Remove node from dialogue and graph
            selectedDialogue.nodes.Remove(dialogueNode);
            this.RemoveFromHierarchy();

            // Delete the node asset
            // Schedule removal so it happens outside the immediate UI event stack
            DialogueEditor.ScheduleDelayedNodeRemoval(dialogueNode, selectedDialogue, "DeleteNode via DialogueNodeView.DeleteNode");
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (selectedDialogue != null && dialogueNode != null)
            {
                selectedDialogue.SetNodePosition(dialogueNode.nodeId, GetPosition());
                EditorUtility.SetDirty(selectedDialogue);
            }
        }

        private void AddChoice()
        {
            if (dialogueNode == null) return;
            dialogueNode.choices.Add(new DialogueChoice { choiceText = "New Choice" });
            RefreshChoices();
            EditorUtility.SetDirty(dialogueNode);
        }

        public void RefreshChoices()
        {
            if (dialogueNode == null) return;

            // Remove any existing edges originating from this node (Next, Choices, Branches)
            var gv = this.GetFirstAncestorOfType<GraphView>();
            if (gv != null)
            {
                var edgesToRemove = gv.edges.ToList().Where(e => e != null && e.output != null && e.output.node == this).ToList();
                foreach (var edge in edgesToRemove)
                {
                    gv.RemoveElement(edge);
                }
            }

            // Remove old choice elements and ports
            foreach (var element in choiceElements)
            {
                if (mainContainer.Contains(element))
                    mainContainer.Remove(element);
            }
            choiceElements.Clear();

            // Remove ALL dynamic ports beyond the first "Next" port
            while (outputContainer.childCount > 1)
            {
                outputContainer.RemoveAt(outputContainer.childCount - 1);
            }

            // Add choice fields and ports
            for (int i = 0; i < dialogueNode.choices.Count; i++)
            {
                var choice = dialogueNode.choices[i];
                var choiceContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var choiceField = new TextField($"Choice {i + 1}") { value = choice.choiceText };
                choiceField.RegisterValueChangedCallback(evt =>
                {
                    choice.choiceText = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                });
                choiceContainer.Add(choiceField);

                int currentIndex = i; // capture index
                var deleteButton = new Button(() => DeleteChoice(currentIndex)) { text = "X" };
                choiceContainer.Add(deleteButton);

                mainContainer.Add(choiceContainer);
                choiceElements.Add(choiceContainer);

                var choicePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueNode));
                choicePort.portName = $"Choice {i + 1}";
                choicePort.userData = i; // Store choice index
                outputContainer.Add(choicePort);
            }

            // Reconnect Next edge if present
            if (gv != null && dialogueNode.nextNode != null)
            {
                var targetView = gv.nodes.OfType<DialogueNodeView>().FirstOrDefault(nv => nv.dialogueNode == dialogueNode.nextNode);
                var outPort = outputContainer[0] as Port; // Next
                var inPort = targetView != null ? targetView.inputContainer[0].Q<Port>() : null;
                if (outPort != null && inPort != null)
                {
                    var edge = outPort.ConnectTo(inPort);
                    gv.AddElement(edge);
                }
            }

            // Reconnect choice edges based on data model
            if (gv != null)
            {
                for (int i = 0; i < dialogueNode.choices.Count; i++)
                {
                    var targetNode = dialogueNode.choices[i].targetNode;
                    if (targetNode == null) continue;
                    var targetView = gv.nodes.OfType<DialogueNodeView>().FirstOrDefault(nv => nv.dialogueNode == targetNode);
                    if (targetView == null) continue;
                    var outPort = outputContainer[i + 1] as Port; // +1 to skip "Next"
                    var inPort = targetView.inputContainer[0].Q<Port>();
                    if (outPort != null && inPort != null)
                    {
                        var edge = outPort.ConnectTo(inPort);
                        gv.AddElement(edge);
                    }
                }
            }

            // Rebuild and reconnect branch ports/edges
            RefreshBranches();

            // Keep character UI consistent
            RefreshCharFields();
        }

        public void RefreshBranches()
        {
            branchContainer.Clear();
            branchElements.Clear();

            // Get GraphView once
            var gv = this.GetFirstAncestorOfType<GraphView>();

            // Remove existing branch edges (keep Next and Choice edges intact)
            if (gv != null)
            {
                var edgesToRemove = gv.edges.ToList().Where(e =>
                    e != null && e.output != null && e.output.node == this &&
                    e.output.userData is int u && u >= dialogueNode.choices.Count // branch ports only
                ).ToList();
                foreach (var edge in edgesToRemove)
                {
                    gv.RemoveElement(edge);
                }
            }

            // Remove existing branch ports (assume branches append after choices, so remove from end)
            while (outputContainer.childCount > dialogueNode.choices.Count + 1) // +1 for Next
            {
                outputContainer.RemoveAt(outputContainer.childCount - 1);
            }
            branchPorts.Clear();

            for (int i = 0; i < dialogueNode.conditionalBranches.Count; i++)
            {
                var branch = dialogueNode.conditionalBranches[i];
                var branchElement = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var nameField = new TextField("Branch Name") { value = branch.branchName };
                nameField.RegisterValueChangedCallback(evt => {
                    branch.branchName = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                    // Update port label immediately if present
                    int portIndex = i + dialogueNode.choices.Count;
                    if (portIndex < outputContainer.childCount)
                    {
                        var port = outputContainer[portIndex] as Port;
                        if (port != null)
                        {
                            port.portName = string.IsNullOrEmpty(evt.newValue) ? $"Branch {i + 1}" : evt.newValue;
                        }
                    }
                });
                branchElement.Add(nameField);

                // Inline operations inspector
                var opsIMGUI = new IMGUIContainer(() =>
                {
                    var so = new SerializedObject(dialogueNode);
                    var branchesProp = so.FindProperty("conditionalBranches");
                    if (i < branchesProp.arraySize)
                    {
                        var el = branchesProp.GetArrayElementAtIndex(i);
                        var ops = el.FindPropertyRelative("operations");
                        EditorGUILayout.PropertyField(ops, new GUIContent("Operations"), true);
                        so.ApplyModifiedProperties();
                    }
                });
                branchElement.Add(opsIMGUI);

                int index = i;
                var deleteButton = new Button(() => DeleteBranch(index)) { text = "X" };
                branchElement.Add(deleteButton);

                branchContainer.Add(branchElement);
                branchElements.Add(branchElement);

                // Create and add port
                var branchPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueNode));
                branchPort.portName = string.IsNullOrEmpty(branch.branchName) ? $"Branch {i + 1}" : branch.branchName;
                branchPort.userData = i + dialogueNode.choices.Count; // Offset index by choice count
                outputContainer.Add(branchPort); // Append after choices
                branchPorts.Add(branchPort);
            }

            // Reconnect branch edges
            if (gv != null)
            {
                for (int i = 0; i < dialogueNode.conditionalBranches.Count; i++)
                {
                    var targetNode = dialogueNode.conditionalBranches[i].targetNode;
                    if (targetNode == null) continue;
                    var targetView = gv.nodes.OfType<DialogueNodeView>().FirstOrDefault(nv => nv.dialogueNode == targetNode);
                    if (targetView == null) continue;
                    int portIndex = 1 + dialogueNode.choices.Count + i; // Next + choices + branch offset
                    var outPort = outputContainer[portIndex] as Port;
                    var inPort = targetView.inputContainer[0].Q<Port>();
                    if (outPort != null && inPort != null)
                    {
                        var edge = outPort.ConnectTo(inPort);
                        gv.AddElement(edge);
                    }
                }
            }
        }

        private void DeleteChoice(int index)
        {
            Debug.Log("Delete attempted at index " + index);
            if (index < 0 || index >= dialogueNode.choices.Count) return;

            // Clear any connections for the choice's port
            var choicePort = outputContainer[index + 1].Q<Port>();
            if (choicePort != null)
            {
                var connections = choicePort.connections.ToList();
                foreach (var edge in connections)
                {
                    edge.input.Disconnect(edge);
                    edge.output.Disconnect(edge);
                    edge.RemoveFromHierarchy();
                }
            }

            // Remove the choice from the data model
            dialogueNode.choices.RemoveAt(index);

            // Refresh the UI and ports
            RefreshChoices();

            // Mark the node and dialogue as dirty to save changes
            EditorUtility.SetDirty(dialogueNode);
            EditorUtility.SetDirty(selectedDialogue);
            AssetDatabase.SaveAssets();
        }

        private void AddBranch()
        {
            dialogueNode.conditionalBranches.Add(new DialogueNode.ConditionalBranch());
            EditorUtility.SetDirty(dialogueNode);
            RefreshBranches();
        }

        private void DeleteBranch(int index)
        {
            if (index < 0 || index >= dialogueNode.conditionalBranches.Count) return;

            // Disconnect port
            if (index < branchPorts.Count)
            {
                var port = branchPorts[index];
                var connections = port.connections.ToList();
                foreach (var edge in connections)
                {
                    edge.input.Disconnect(edge);
                    edge.output.Disconnect(edge);
                    edge.parent.Remove(edge);
                }
            }

            dialogueNode.conditionalBranches.RemoveAt(index);
            EditorUtility.SetDirty(dialogueNode);
            RefreshBranches();
        }

        // UPDATED: Generalize for both choices and branches
        public int GetOutputPortIndex(Port port)
        {
            if (port.userData is int index) return index;
            return -1; // For nextNode or invalid
        }
    }
}