using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;

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

        [MenuItem("Window/Dialogue Editor")]
        public static void Open()
        {
            GetWindow<DialogueEditor>("Dialogue Editor");
        }

        private void OnEnable()
        {
            // ConstructGraphView();
            // AddToolbar();
            rootVisualElement.Clear();

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.flexGrow = 1;

            AddToolbar(container); // Ensure called—toolbar fix

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

            AddDialogueDefaultsUI(container);
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

            var loadButton = new Button(LoadDialogue) { text = "Load Dialogue" }; // Implement LoadDialogue() to load SO, clear/add nodes
            var saveButton = new Button(SaveDialogue) { text = "Save" }; // AssetDatabase.SaveAssets();
            var previewButton = new Button(PreviewDialogue) { text = "Preview" }; // Runtime test
            var rebuildButton = new Button(RebuildGraph) { text = "Rebuild Graph" }; // NEW: For recovery

            // Add with flex for spacing
            loadButton.style.flexGrow = 1;
            saveButton.style.flexGrow = 1;
            previewButton.style.flexGrow = 1;
            rebuildButton.style.flexGrow = 1;

            toolbar.Add(loadButton);
            toolbar.Add(saveButton);
            toolbar.Add(previewButton);
            toolbar.Add(rebuildButton);

            parent.Add(toolbar);
        }

        private void AddDialogueDefaultsUI(VisualElement parent)
        {
            if (selectedDialogue == null) return;

            var defaultsContainer = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 10 } };

            var speakerField = new ObjectField("Default Speaker") { objectType = typeof(Character), value = selectedDialogue.defaultSpeaker };
            speakerField.RegisterValueChangedCallback(evt =>
            {
                selectedDialogue.defaultSpeaker = evt.newValue as Character;
                EditorUtility.SetDirty(selectedDialogue);
            });
            defaultsContainer.Add(speakerField);

            var speakerNameField = new TextField("Default Speaker Name") { value = selectedDialogue.defaultSpeakerName };
            speakerNameField.RegisterValueChangedCallback(evt =>
            {
                selectedDialogue.defaultSpeakerName = evt.newValue;
                EditorUtility.SetDirty(selectedDialogue);
            });
            defaultsContainer.Add(speakerNameField);

            var listenerField = new ObjectField("Default Listener") { objectType = typeof(Character), value = selectedDialogue.defaultListener };

            var showListenerToggle = new Toggle("Show Default Listener") { value = selectedDialogue.showDefaultListener };
            showListenerToggle.RegisterValueChangedCallback(evt =>
            {
                selectedDialogue.showDefaultListener = evt.newValue;
                EditorUtility.SetDirty(selectedDialogue);
                listenerField.SetEnabled(evt.newValue);
            });
            defaultsContainer.Add(showListenerToggle);

            listenerField.SetEnabled(selectedDialogue.showDefaultListener);
            listenerField.RegisterValueChangedCallback(evt =>
            {
                selectedDialogue.defaultListener = evt.newValue as Character;
                EditorUtility.SetDirty(selectedDialogue);
            });
            defaultsContainer.Add(listenerField);

            parent.Add(defaultsContainer);
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

            var nodeView = new DialogueNodeView(dialogueNode, selectedDialogue, this)
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
            if (change.elementsToRemove != null)
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
                            DestroyImmediate(nodeView.dialogueNode, true);
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



        public void RebuildGraph()
        {
            graphView.DeleteElements(graphView.graphElements.ToList()); // Clear all

            if (selectedDialogue == null) return;

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

        public List<Character> GetAllCharacters()
        {
            var guids = AssetDatabase.FindAssets("t:Character");
            var characters = new List<Character>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var character = AssetDatabase.LoadAssetAtPath<Character>(path);
                if (character != null)
                    characters.Add(character);
            }
            return characters;
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
        private readonly DialogueEditor editor;
        private VisualElement speakerContainer; // To group speaker-related fields
        private VisualElement listenerContainer; // Similar for listener
        private List<VisualElement> expressionElements = new List<VisualElement>(); // For dynamic refresh

        public DialogueNodeView(DialogueNode node, Dialogue dialogue, DialogueEditor editor)
        {
            dialogueNode = node;
            selectedDialogue = dialogue;
            this.editor = editor;

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

            // // Node fields (basic; detailed editing via Inspector)
            // speakerField = new TextField("Speaker Name") { value = dialogueNode.speakerName };
            // speakerField.RegisterValueChangedCallback(evt =>
            // {
            //     dialogueNode.speakerName = evt.newValue;
            //     title = string.IsNullOrEmpty(evt.newValue) ? "Node" : evt.newValue;
            //     EditorUtility.SetDirty(dialogueNode);
            // });
            // mainContainer.Add(speakerField);

            // charField = new ObjectField("Speaker") { objectType = typeof(Character), value = dialogueNode.speakerCharacter };
            // charField.RegisterValueChangedCallback(evt =>
            // {
            //     dialogueNode.speakerCharacter = evt.newValue as Character;
            //     EditorUtility.SetDirty(dialogueNode);
            //     RefreshCharFields();
            // });
            // mainContainer.Add(charField);

            // /// ----- ADD LISTENER CHARACTER ------ ///
            // showListenerToggle = new Toggle("Show Listener") { value = dialogueNode.listenerCharacter != null };
            // showListenerToggle.RegisterValueChangedCallback(evt =>
            // {
            //     if (!evt.newValue)
            //     {
            //         dialogueNode.listenerCharacter = null;
            //         dialogueNode.listenerExpression = null;
            //     }
            //     EditorUtility.SetDirty(dialogueNode);
            //     RefreshCharFields();
            // });
            // mainContainer.Add(showListenerToggle);

            // listenerField = new ObjectField("Listener") { objectType = typeof(Character), value = dialogueNode.listenerCharacter };
            // listenerField.RegisterValueChangedCallback(evt =>
            // {
            //     dialogueNode.listenerCharacter = evt.newValue as Character;
            //     EditorUtility.SetDirty(dialogueNode);
            //     RefreshCharFields();
            // });
            // //mainContainer.Add(listenerField);

            
            // /// ----- END OF LISTENER CHARACTER ----- ///

            string[] characterGuids = AssetDatabase.FindAssets("t:Character");
            List<Character> characterChoices = characterGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<Character>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(c => c != null)
                .ToList();

            if (characterChoices.Count == 0)
            {
                // Handle empty list gracefully (e.g., fallback to ObjectField or show warning)
                var warningLabel = new Label("No Character assets found in project.");
                // Add to your container...
                return;
            }

            // Compute default index safely
            int defaultSpeakerIndex = 0; // Fallback to first character if null or not found
            if (dialogueNode.speakerCharacter != null)
            {
                defaultSpeakerIndex = characterChoices.IndexOf(dialogueNode.speakerCharacter);
                if (defaultSpeakerIndex == -1)
                {
                    defaultSpeakerIndex = 0; // Not in list? Fallback to first
                }
            }

            // Create the PopupField using index overload
            var speakerField = new PopupField<Character>("Speaker Character", characterChoices, defaultSpeakerIndex);
            speakerField.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.speakerCharacter = evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
                RefreshExpressions(); // Custom method to refresh expression fields (see below)
            });

            var speakerOverrideToggle = new Toggle("Override Speaker") { value = dialogueNode.overrideSpeaker };
            speakerOverrideToggle.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.overrideSpeaker = evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
                RefreshCharFields();
            });
            mainContainer.Add(speakerOverrideToggle);

            var allCharacters = editor.GetAllCharacters(); // Should return List<Character>
            var defaultSpeaker = selectedDialogue.defaultSpeaker ?? (allCharacters.Count > 0 ? allCharacters[0] : null);

            var speakerDropdown = new PopupField<Character>(
                "Speaker",
                allCharacters,
                defaultSpeaker
            );
            speakerDropdown.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.speakerCharacter = evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
            });
            mainContainer.Add(speakerDropdown);

            // Repeat similar for listener
            var listenerOverrideToggle = new Toggle("Override Listener") { value = dialogueNode.overrideListener };
            listenerOverrideToggle.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.overrideListener = evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
                RefreshCharFields();
            });
            mainContainer.Add(listenerOverrideToggle);

            var listenerDropdown = new PopupField<Character>(
                "Listener",
                allCharacters,
                dialogueNode.overrideListener ? dialogueNode.listenerCharacter : selectedDialogue.defaultListener
            );
            listenerDropdown.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.listenerCharacter = evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
            });
            mainContainer.Add(listenerDropdown);
            

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

            speakerContainer = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            listenerContainer = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            mainContainer.Add(speakerContainer);
            mainContainer.Add(listenerContainer);

            RefreshCharFields(); // Call at end of ctor
            
        }

        // void RefreshCharFields()
        // {
        //     // if (expressionDropdown != null)
        //     //     mainContainer.Remove(expressionDropdown);

        //     // if (dialogueNode.character != null)
        //     // {
        //     //     var expressions = dialogueNode.character.expressions.ConvertAll(e => e.expressionName);
        //     //     if (!expressions.Contains("Default")) expressions.Insert(0, "Default");

        //     //     // Ensure the current value is valid
        //     //     string currentValue = dialogueNode.charExpression;
        //     //     if (string.IsNullOrEmpty(currentValue) || !expressions.Contains(currentValue))
        //     //     {
        //     //         currentValue = "Default";
        //     //         dialogueNode.charExpression = currentValue;
        //     //     }

        //     //     expressionDropdown = new PopupField<string>("Expression", expressions, currentValue);
        //     //     expressionDropdown.RegisterValueChangedCallback(e =>
        //     //     {
        //     //         dialogueNode.charExpression = e.newValue;
        //     //         EditorUtility.SetDirty(dialogueNode);
        //     //     });
        //     //     mainContainer.Add(expressionDropdown);
        //     // }


        //     // Remove old dropdown if present
        //     if (expressionDropdown != null)
        //     {
        //         mainContainer.Remove(expressionDropdown);
        //         expressionDropdown = null;
        //     }

        //     // Remove listener UI if present
        //     if (listenerField != null && mainContainer.Contains(listenerField))
        //         mainContainer.Remove(listenerField);
        //     if (listenerExpressionDropdown != null && mainContainer.Contains(listenerExpressionDropdown))
        //         mainContainer.Remove(listenerExpressionDropdown);

        //     if (dialogueNode.speakerCharacter != null)
        //     {
        //         // Overwrite speaker name with character SO name and disable editing
        //         speakerField.value = dialogueNode.speakerCharacter.npcName;
        //         speakerField.SetEnabled(false);

        //         dialogueNode.speakerName = dialogueNode.speakerCharacter.npcName;
        //         EditorUtility.SetDirty(dialogueNode);

        //         // Build expression list
        //         var expressions = dialogueNode.speakerCharacter.expressions.ConvertAll(e => e.expressionName);
        //         if (!expressions.Contains("Default")) expressions.Insert(0, "Default");

        //         // Ensure current value is valid
        //         string currentValue = dialogueNode.speakerExpression;
        //         if (string.IsNullOrEmpty(currentValue) || !expressions.Contains(currentValue))
        //         {
        //             currentValue = "Default";
        //             dialogueNode.speakerExpression = currentValue;
        //         }

        //         expressionDropdown = new PopupField<string>("Expression", expressions, currentValue);
        //         expressionDropdown.RegisterValueChangedCallback(e =>
        //         {
        //             dialogueNode.speakerExpression = e.newValue;
        //             EditorUtility.SetDirty(dialogueNode);
        //         });

        //         // Insert directly after the character field
        //         mainContainer.Insert(mainContainer.IndexOf(charField) + 1, expressionDropdown);
        //     }
        //     else
        //     {
        //         // No character: enable speaker field for manual entry
        //         speakerField.SetEnabled(true);
        //     }

        //     // Expression dropdown for listener (similar to speaker)
        //     if (showListenerToggle.value)
        //     {
        //         if (!mainContainer.Contains(listenerField))
        //             mainContainer.Add(listenerField);

        //         if (dialogueNode.listenerCharacter != null)
        //         {
        //             var expressions = dialogueNode.listenerCharacter.expressions.ConvertAll(e => e.expressionName);
        //             if (!expressions.Contains("Default")) expressions.Insert(0, "Default");
        //             string currentValue = dialogueNode.listenerExpression;
        //             if (string.IsNullOrEmpty(currentValue) || !expressions.Contains(currentValue))
        //             {
        //                 currentValue = "Default";
        //                 dialogueNode.listenerExpression = currentValue;
        //             }
        //             listenerExpressionDropdown = new PopupField<string>("Listener Expression", expressions, currentValue);
        //             listenerExpressionDropdown.RegisterValueChangedCallback(e =>
        //             {
        //                 dialogueNode.listenerExpression = e.newValue;
        //                 EditorUtility.SetDirty(dialogueNode);
        //             });
        //             mainContainer.Add(listenerExpressionDropdown);
        //         }
        //     }

        //     // Toggle to turn on the listener as the speaker
        //     if (listenerExpressionDropdown != null)
        //     {
        //         // Add "Is Speaker" toggle below listener expression
        //         var isSpeakerToggle = new Toggle("Is Speaker") { value = dialogueNode.listenerIsSpeaker };
        //         isSpeakerToggle.RegisterValueChangedCallback(e =>
        //         {
        //             dialogueNode.listenerIsSpeaker = e.newValue;
        //             EditorUtility.SetDirty(dialogueNode);

        //             // If checked, override speaker name in editor UI
        //             if (e.newValue && dialogueNode.listenerCharacter != null)
        //             {
        //                 speakerField.value = dialogueNode.listenerCharacter.npcName;
        //                 speakerField.SetEnabled(false);
        //                 dialogueNode.speakerName = dialogueNode.listenerCharacter.npcName;
        //             }
        //             else
        //             {
        //                 // Restore speaker field based on speakerCharacter
        //                 if (dialogueNode.speakerCharacter != null)
        //                 {
        //                     speakerField.value = dialogueNode.speakerCharacter.npcName;
        //                     speakerField.SetEnabled(false);
        //                     dialogueNode.speakerName = dialogueNode.speakerCharacter.npcName;
        //                 }
        //                 else
        //                 {
        //                     speakerField.SetEnabled(true);
        //                 }
        //             }
        //         });
        //         mainContainer.Add(isSpeakerToggle);

        //         // Initial override if already checked
        //         if (dialogueNode.listenerIsSpeaker && dialogueNode.listenerCharacter != null)
        //         {
        //             speakerField.value = dialogueNode.listenerCharacter.npcName;
        //             speakerField.SetEnabled(false);
        //             dialogueNode.speakerName = dialogueNode.listenerCharacter.npcName;
        //         }
        //     }
        // }

        private void RefreshCharFields()
        {
            speakerContainer.Clear();
            listenerContainer.Clear();

            // Load available characters (cached or reloaded as needed)
            string[] characterGuids = AssetDatabase.FindAssets("t:Character");
            List<Character> characterChoices = characterGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<Character>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(c => c != null)
                .ToList();

            if (characterChoices.Count == 0)
            {
                var warningLabel = new Label("No Character assets found in project.");
                speakerContainer.Add(warningLabel);
                listenerContainer.Add(new Label("No Character assets found in project.")); // Duplicate for listener
                return;
            }

            // Speaker Override Toggle
            var overrideSpeakerToggle = new Toggle("Override Speaker") { value = dialogueNode.overrideSpeaker };
            overrideSpeakerToggle.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.overrideSpeaker = evt.newValue;
                if (evt.newValue && dialogueNode.speakerCharacter == null)
                {
                    dialogueNode.speakerCharacter = selectedDialogue.defaultSpeaker; // Auto-set to default for convenience
                }
                EditorUtility.SetDirty(dialogueNode);
                RefreshCharFields(); // Rebuild UI
            });
            speakerContainer.Add(overrideSpeakerToggle);

            if (dialogueNode.overrideSpeaker)
            {
                // Compute default index safely for speaker
                int defaultSpeakerIndex = 0;
                if (dialogueNode.speakerCharacter != null)
                {
                    defaultSpeakerIndex = characterChoices.IndexOf(dialogueNode.speakerCharacter);
                    if (defaultSpeakerIndex == -1) defaultSpeakerIndex = 0;
                }

                // Create PopupField for speaker
                var speakerField = new PopupField<Character>("Speaker Character", characterChoices, defaultSpeakerIndex);
                speakerField.RegisterValueChangedCallback(evt =>
                {
                    dialogueNode.speakerCharacter = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                    RefreshExpressions();
                });
                speakerContainer.Add(speakerField);
            }
            else
            {
                // Show read-only default
                string defaultName = selectedDialogue.defaultSpeaker != null ? selectedDialogue.defaultSpeaker.npcName : "None";
                var defaultLabel = new Label($"Using Default Speaker: {defaultName}");
                speakerContainer.Add(defaultLabel);
            }

            // Listener Override Toggle
            var overrideListenerToggle = new Toggle("Override Listener") { value = dialogueNode.overrideListener };
            overrideListenerToggle.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.overrideListener = evt.newValue;
                if (evt.newValue && dialogueNode.listenerCharacter == null)
                {
                    dialogueNode.listenerCharacter = selectedDialogue.defaultListener; // Auto-set to default for convenience
                }
                EditorUtility.SetDirty(dialogueNode);
                RefreshCharFields(); // Rebuild UI
            });
            listenerContainer.Add(overrideListenerToggle);

            if (dialogueNode.overrideListener || selectedDialogue.showDefaultListener)
            {
                // Compute default index safely for listener
                int defaultListenerIndex = 0;
                if (dialogueNode.listenerCharacter != null)
                {
                    defaultListenerIndex = characterChoices.IndexOf(dialogueNode.listenerCharacter);
                    if (defaultListenerIndex == -1) defaultListenerIndex = 0;
                }

                // Create PopupField for listener
                var listenerField = new PopupField<Character>("Listener Character", characterChoices, defaultListenerIndex);
                listenerField.RegisterValueChangedCallback(evt =>
                {
                    dialogueNode.listenerCharacter = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                    RefreshExpressions();
                });
                listenerContainer.Add(listenerField);
            }
            else
            {
                // Show read-only default or hide
                string defaultName = selectedDialogue.defaultListener != null ? selectedDialogue.defaultListener.npcName : "None";
                var defaultLabel = new Label($"Using Default Listener: {defaultName} (Shown: {selectedDialogue.showDefaultListener})");
                listenerContainer.Add(defaultLabel);
            }

            RefreshExpressions(); // Always refresh expressions after characters
        }

        private void RefreshExpressions()
        {
            // Clear old expression elements
            foreach (var elem in expressionElements)
            {
                mainContainer.Remove(elem);
            }
            expressionElements.Clear();

            // Effective Speaker
            Character effectiveSpeaker = dialogueNode.overrideSpeaker ? dialogueNode.speakerCharacter : selectedDialogue.defaultSpeaker;
            if (effectiveSpeaker != null)
            {
                List<string> expChoices = effectiveSpeaker.expressions.Select(e => e.expressionName).ToList();
                if (!expChoices.Contains("default")) expChoices.Insert(0, "default"); // Ensure a fallback

                string defExp = string.IsNullOrEmpty(dialogueNode.speakerExpression) ? "default" : dialogueNode.speakerExpression;
                int defaultIndex = expChoices.IndexOf(defExp);
                if (defaultIndex == -1) defaultIndex = 0;

                var speakerExpField = new PopupField<string>("Speaker Expression", expChoices, defaultIndex);
                speakerExpField.RegisterValueChangedCallback(evt =>
                {
                    dialogueNode.speakerExpression = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                });
                mainContainer.Add(speakerExpField);
                expressionElements.Add(speakerExpField);
            }
            else
            {
                var noSpeakerLabel = new Label("No speaker selected - cannot choose expression.");
                mainContainer.Add(noSpeakerLabel);
                expressionElements.Add(noSpeakerLabel);
            }

            // Effective Listener
            Character effectiveListener = dialogueNode.overrideListener ? dialogueNode.listenerCharacter : selectedDialogue.defaultListener;
            if (effectiveListener != null && (dialogueNode.overrideListener || selectedDialogue.showDefaultListener))
            {
                List<string> expChoices = effectiveListener.expressions.Select(e => e.expressionName).ToList();
                if (!expChoices.Contains("default")) expChoices.Insert(0, "default"); // Ensure a fallback

                string defExp = string.IsNullOrEmpty(dialogueNode.listenerExpression) ? "default" : dialogueNode.listenerExpression;
                int defaultIndex = expChoices.IndexOf(defExp);
                if (defaultIndex == -1) defaultIndex = 0;

                var listenerExpField = new PopupField<string>("Listener Expression", expChoices, defaultIndex);
                listenerExpField.RegisterValueChangedCallback(evt =>
                {
                    dialogueNode.listenerExpression = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                });
                mainContainer.Add(listenerExpField);
                expressionElements.Add(listenerExpField);

                // Listener is Speaker Toggle (only show if listener is present)
                var listenerIsSpeakerToggle = new Toggle("Listener is Speaker") { value = dialogueNode.listenerIsSpeaker };
                listenerIsSpeakerToggle.RegisterValueChangedCallback(evt =>
                {
                    dialogueNode.listenerIsSpeaker = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                });
                mainContainer.Add(listenerIsSpeakerToggle);
                expressionElements.Add(listenerIsSpeakerToggle);
            }
            else if (selectedDialogue.showDefaultListener)
            {
                var noListenerLabel = new Label("No listener selected - cannot choose expression.");
                mainContainer.Add(noListenerLabel);
                expressionElements.Add(noListenerLabel);
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
            AssetDatabase.RemoveObjectFromAsset(dialogueNode);
            EditorUtility.SetDirty(selectedDialogue);
            AssetDatabase.SaveAssets();

            // Destroy the node object (Unity API call)
            if (dialogueNode != null)
            {
                UnityEngine.Object.DestroyImmediate(dialogueNode);
            }
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
            // Remove old choice elements and ports
            foreach (var element in choiceElements)
            {
                mainContainer.Remove(element);
            }
            choiceElements.Clear();

            // Remove existing choice ports (keep the "Next" port at index 0)
            while (outputContainer.childCount > 1)
            {
                outputContainer.RemoveAt(outputContainer.childCount - 1);
            }

            // Add choice fields and ports
            for (int i = 0; i < dialogueNode.choices.Count; i++)
            {
                var choice = dialogueNode.choices[i];
                var choiceContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var choiceField = new TextField("Choice") { value = choice.choiceText };
                choiceField.RegisterValueChangedCallback(evt =>
                {
                    choice.choiceText = evt.newValue;
                    EditorUtility.SetDirty(dialogueNode);
                });
                choiceContainer.Add(choiceField);

                int currentIndex = i; // Local variable to capture the correct index in the lambda
                var deleteButton = new Button(() => DeleteChoice(currentIndex)) { text = "X" };
                choiceContainer.Add(deleteButton);

                mainContainer.Add(choiceContainer);
                choiceElements.Add(choiceContainer);

                var choicePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueNode));
                choicePort.portName = $"Choice {i + 1}";
                choicePort.userData = i; // Store choice index
                outputContainer.Add(choicePort);
            }
            
            RefreshCharFields();
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

        public void RefreshBranches()
        {
            branchContainer.Clear();
            branchElements.Clear();

            // NEW: Remove existing branch ports (assume branches append after choices, so remove from end)
            while (outputContainer.childCount > dialogueNode.choices.Count + 1) // +1 for nextNode if present
            {
                outputContainer.RemoveAt(outputContainer.childCount - 1);
            }
            branchPorts.Clear();

            for (int i = 0; i < dialogueNode.conditionalBranches.Count; i++)
            {
                var branch = dialogueNode.conditionalBranches[i];
                var branchElement = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var nameField = new TextField("Branch Name") { value = branch.branchName };
                nameField.RegisterValueChangedCallback(evt => { branch.branchName = evt.newValue; EditorUtility.SetDirty(dialogueNode); });
                branchElement.Add(nameField);

                var condField = new ObjectField("Condition") { objectType = typeof(Condition), value = branch.condition };
                condField.RegisterValueChangedCallback(evt => { branch.condition = evt.newValue as Condition; EditorUtility.SetDirty(dialogueNode); });
                branchElement.Add(condField);

                int index = i;
                var deleteButton = new Button(() => DeleteBranch(index)) { text = "X" };
                branchElement.Add(deleteButton);

                branchContainer.Add(branchElement);
                branchElements.Add(branchElement);

                // Create and add port
                var branchPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueNode));
                branchPort.portName = branch.branchName ?? $"Branch {i + 1}";
                branchPort.userData = i + dialogueNode.choices.Count; // Offset index by choice count
                outputContainer.Add(branchPort); // Append after choices
                branchPorts.Add(branchPort);
            }

            RefreshCharFields();
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