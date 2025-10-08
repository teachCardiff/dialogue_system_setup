// using UnityEditor;
// using UnityEditor.Experimental.GraphView;
// using UnityEngine;
// using UnityEngine.UIElements;
// using System;
// using System.Linq;
// using System.Collections.Generic;

// namespace DialogueSystem.Editor
// {
//     /// <summary>
//     /// Custom Editor Window for visual dialogue editing.
//     /// Provides a node-based interface for designers to create/edit dialogue trees.
//     /// Open via Menu: Window > Dialogue Editor.
//     /// </summary>
//     public class DialogueEditor : EditorWindow
//     {
//         public Dialogue selectedDialogue;
//         private DialogueGraphView graphView;

//         [MenuItem("Window/Dialogue Editor")]
//         public static void Open()
//         {
//             GetWindow<DialogueEditor>("Dialogue Editor");
//         }

//         private void OnEnable()
//         {
//             ConstructGraphView();
//             AddToolbar();
//         }

//         private void OnDisable()
//         {
//             rootVisualElement.Remove(graphView);
//         }

//         private void ConstructGraphView()
//         {
//             graphView = new DialogueGraphView(this)
//             {
//                 name = "Dialogue Graph",
//                 style = { flexGrow = 1 }
//             };
//             graphView.StretchToParentSize();
//             rootVisualElement.Add(graphView);

//             graphView.nodeCreationRequest = context => CreateNode(context.screenMousePosition);
//             graphView.graphViewChanged = OnGraphChanged;

//             // Enable right-click context menu for creating nodes
//             graphView.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
//         }

//         private void BuildContextualMenu(ContextualMenuPopulateEvent evt)
//         {
//             evt.menu.AppendAction("Create Node", a => CreateNode(evt.localMousePosition));
//         }

//         private void AddToolbar()
//         {
//             var toolbar = new VisualElement
//             {
//                 style = {
//                     flexDirection = FlexDirection.Row,
//                     paddingTop = 5,
//                     paddingBottom = 5,
//                     backgroundColor = new Color(0.2f, 0.2f, 0.2f)
//                 }
//             };

//             var loadButton = new Button(() => LoadDialogue()) { text = "Load Dialogue" };
//             var saveButton = new Button(() => SaveDialogue()) { text = "Save" };
//             var previewButton = new Button(() => PreviewDialogue()) { text = "Preview" };
//             var resetButton = new Button(() => { if (selectedDialogue != null) selectedDialogue.ResetProgress(); EditorUtility.SetDirty(selectedDialogue); }) { text = "Reset Progress" };

//             toolbar.Add(loadButton);
//             toolbar.Add(saveButton);
//             toolbar.Add(previewButton);
//             toolbar.Add(resetButton);
//             rootVisualElement.Add(toolbar);
//         }

//         public DialogueNodeView CreateNode(Vector2 screenPosition)
//         {
//             if (selectedDialogue == null)
//             {
//                 Debug.LogError("Please load a Dialogue asset first before creating nodes.");
//                 return null;
//             }

//             // Convert screen position to graph view's local coordinates
//             var localPosition = graphView.contentViewContainer.WorldToLocal(screenPosition);

//             var node = ScriptableObject.CreateInstance<DialogueNode>();
//             node.nodeId = Guid.NewGuid().ToString();
//             node.name = $"Node_{selectedDialogue.nodes.Count}";
//             AssetDatabase.AddObjectToAsset(node, selectedDialogue);
//             selectedDialogue.nodes.Add(node);
//             selectedDialogue.SetNodePosition(node.nodeId, new Rect(localPosition, new Vector2(250, 300)));
//             EditorUtility.SetDirty(selectedDialogue);
//             AssetDatabase.SaveAssets();

//             var nodeView = AddNodeToGraph(node, localPosition);
//             return nodeView;
//         }

//         private DialogueNodeView AddNodeToGraph(DialogueNode dialogueNode, Vector2 position)
//         {
//             // Load saved position if available
//             var savedPos = selectedDialogue.GetNodePosition(dialogueNode.nodeId);
//             var finalPos = savedPos != Vector2.zero ? savedPos : position;

//             var nodeView = new DialogueNodeView(dialogueNode, selectedDialogue)
//             {
//                 title = dialogueNode.speakerName != "" ? dialogueNode.speakerName : "Node"
//             };
//             nodeView.SetPosition(new Rect(finalPos, new Vector2(250, 300)));
//             graphView.AddElement(nodeView);

//             // Refresh choices display
//             nodeView.RefreshChoices();

//             return nodeView;
//         }

//         private GraphViewChange OnGraphChanged(GraphViewChange change)
//         {
//             // Handle edge creation (links between nodes)
//             if (change.edgesToCreate != null)
//             {
//                 foreach (var edge in change.edgesToCreate)
//                 {
//                     var outputNode = edge.output.node as DialogueNodeView;
//                     var inputNode = edge.input.node as DialogueNodeView;
//                     if (outputNode != null && inputNode != null)
//                     {
//                         // Check if this is a choice connection or nextNode
//                         int choiceIndex = outputNode.GetOutputPortChoiceIndex(edge.output);
//                         if (choiceIndex >= 0)
//                         {
//                             // Connect as a choice target
//                             outputNode.dialogueNode.choices[choiceIndex].targetNode = inputNode.dialogueNode;
//                         }
//                         else
//                         {
//                             // Linear connection
//                             outputNode.dialogueNode.nextNode = inputNode.dialogueNode;
//                         }
//                         EditorUtility.SetDirty(selectedDialogue);
//                     }
//                 }
//             }

//             // Handle node movement to save positions
//             if (change.movedElements != null)
//             {
//                 foreach (var element in change.movedElements)
//                 {
//                     if (element is DialogueNodeView nodeView)
//                     {
//                         selectedDialogue.SetNodePosition(nodeView.dialogueNode.nodeId, nodeView.GetPosition());
//                         EditorUtility.SetDirty(selectedDialogue);
//                     }
//                 }
//             }

//             return change;
//         }

//         private void LoadDialogue()
//         {
//             var path = EditorUtility.OpenFilePanel("Load Dialogue", "Assets", "asset");
//             if (!string.IsNullOrEmpty(path))
//             {
//                 path = path.Replace(Application.dataPath, "Assets");
//                 selectedDialogue = AssetDatabase.LoadAssetAtPath<Dialogue>(path);
//                 if (selectedDialogue != null)
//                 {
//                     RebuildGraph();
//                 }
//                 else
//                 {
//                     Debug.LogError("Selected file is not a Dialogue asset.");
//                 }
//             }
//         }

//         private void RebuildGraph()
//         {
//             graphView.DeleteElements(graphView.graphElements.ToList());
//             if (selectedDialogue == null) return;

//             // Create node views with saved positions
//             foreach (var node in selectedDialogue.nodes)
//             {
//                 AddNodeToGraph(node, Vector2.zero);
//             }

//             // Reconnect edges
//             foreach (var node in selectedDialogue.nodes)
//             {
//                 var nodeView = graphView.nodes.ToList().OfType<DialogueNodeView>()
//                     .FirstOrDefault(n => n.dialogueNode == node);
//                 if (nodeView == null) continue;

//                 // Linear connection
//                 if (node.nextNode != null)
//                 {
//                     var targetView = graphView.nodes.ToList().OfType<DialogueNodeView>()
//                         .FirstOrDefault(n => n.dialogueNode == node.nextNode);
//                     if (targetView != null)
//                     {
//                         var edge = nodeView.outputContainer[0].Q<Port>().ConnectTo(targetView.inputContainer[0].Q<Port>());
//                         graphView.AddElement(edge);
//                     }
//                 }

//                 // Choice connections
//                 for (int i = 0; i < node.choices.Count; i++)
//                 {
//                     var choice = node.choices[i];
//                     if (choice.targetNode != null)
//                     {
//                         var targetView = graphView.nodes.ToList().OfType<DialogueNodeView>()
//                             .FirstOrDefault(n => n.dialogueNode == choice.targetNode);
//                         if (targetView != null)
//                         {
//                             var choicePort = nodeView.outputContainer[i + 1].Q<Port>();
//                             var edge = choicePort.ConnectTo(targetView.inputContainer[0].Q<Port>());
//                             graphView.AddElement(edge);
//                         }
//                     }
//                 }
//             }
//         }

//         private void SaveDialogue()
//         {
//             if (selectedDialogue != null)
//             {
//                 EditorUtility.SetDirty(selectedDialogue);
//                 AssetDatabase.SaveAssets();
//                 Debug.Log("Dialogue saved.");
//             }
//             else
//             {
//                 Debug.LogWarning("No dialogue selected to save.");
//             }
//         }

//         private void PreviewDialogue()
//         {
//             if (selectedDialogue?.startNode == null)
//             {
//                 Debug.LogWarning("No start node set for preview.");
//                 return;
//             }

//             Debug.Log("Dialogue Preview:");
//             var node = selectedDialogue.startNode;
//             int depth = 0;
//             while (node != null && depth < 100) // Prevent infinite loops
//             {
//                 Debug.Log($"{node.speakerName}: {node.dialogueText}");
//                 if (node.choices.Count > 0)
//                 {
//                     Debug.Log("Choices: " + string.Join(", ", node.choices.Select(c => c.choiceText)));
//                 }
//                 node = node.nextNode; // Simplified: follows linear path
//                 depth++;
//             }
//         }
//     }

//     /// <summary>
//     /// Custom GraphView for dialogue nodes.
//     /// Handles node rendering, connections, and custom deletion behavior.
//     /// </summary>
//     public class DialogueGraphView : GraphView
//     {
//         private readonly DialogueEditor editor;

//         public DialogueGraphView(DialogueEditor editor)
//         {
//             this.editor = editor;
//             SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
//             this.AddManipulator(new ContentDragger());
//             this.AddManipulator(new SelectionDragger());
//             this.AddManipulator(new RectangleSelector());

//             // Add grid background
//             var grid = new GridBackground();
//             Insert(0, grid);
//             grid.StretchToParentSize();

//             // Set up edge connector for creating nodes on drop
//             var edgeConnector = new EdgeConnector<Edge>(new CustomEdgeConnectorListener(this, editor));
//             this.AddManipulator(edgeConnector);

//             // Hook into deletion to handle node removal from ScriptableObject
//             deleteSelection = (operation, askUser) =>
//             {
//                 // Filter nodes to delete, excluding start node
//                 var nodesToDelete = selection.OfType<DialogueNodeView>()
//                     .Where(nodeView => editor.selectedDialogue != null && nodeView.dialogueNode != editor.selectedDialogue.startNode)
//                     .ToList();

//                 // Delete nodes using DialogueNodeView.DeleteNode
//                 foreach (var nodeView in nodesToDelete)
//                 {
//                     nodeView.DeleteNode();
//                 }

//                 // Delete edges
//                 var edgesToDelete = selection.OfType<Edge>().ToList();
//                 foreach (var edge in edgesToDelete)
//                 {
//                     if (edge.output != null && edge.input != null)
//                     {
//                         var outputNode = edge.output.node as DialogueNodeView;
//                         var inputNode = edge.input.node as DialogueNodeView;
//                         if (outputNode != null && inputNode != null)
//                         {
//                             int choiceIndex = outputNode.GetOutputPortChoiceIndex(edge.output);
//                             if (choiceIndex >= 0)
//                             {
//                                 outputNode.dialogueNode.choices[choiceIndex].targetNode = null;
//                             }
//                             else
//                             {
//                                 outputNode.dialogueNode.nextNode = null;
//                             }
//                             EditorUtility.SetDirty(outputNode.dialogueNode);
//                         }
//                     }
//                     RemoveElement(edge);
//                 }
//             };
//         }

//         public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
//         {
//             var compatiblePorts = new List<Port>();
//             foreach (var port in ports.ToList())
//             {
//                 if (startPort != port && startPort.node != port.node &&
//                     startPort.direction != port.direction)
//                 {
//                     compatiblePorts.Add(port);
//                 }
//             }
//             return compatiblePorts;
//         }
//     }

//     /// <summary>
//     /// Custom listener for edge connections, including creating new nodes on drop outside ports.
//     /// </summary>
//     public class CustomEdgeConnectorListener : IEdgeConnectorListener
//     {
//         private readonly GraphView graphView;
//         private readonly DialogueEditor editor;

//         public CustomEdgeConnectorListener(GraphView graphView, DialogueEditor editor)
//         {
//             this.graphView = graphView;
//             this.editor = editor;
//         }

//         public void OnDropOutsidePort(Edge edge, Vector2 position)
//         {
//             var outputNode = edge.output.node as DialogueNodeView;
//             if (outputNode == null) return;

//             // Convert drop position to graph view's local coordinates
//             var localPosition = graphView.contentViewContainer.WorldToLocal(position);

//             // Create a new node at the drop position
//             var newNodeView = editor.CreateNode(localPosition);
//             if (newNodeView != null)
//             {
//                 // Connect the edge to the new node
//                 var inputPort = newNodeView.inputContainer[0].Q<Port>();
//                 edge.input = inputPort;
//                 inputPort.Connect(edge);
//                 graphView.AddElement(edge);

//                 // Update the dialogue node connections
//                 int choiceIndex = outputNode.GetOutputPortChoiceIndex(edge.output);
//                 if (choiceIndex >= 0)
//                 {
//                     outputNode.dialogueNode.choices[choiceIndex].targetNode = newNodeView.dialogueNode;
//                 }
//                 else
//                 {
//                     outputNode.dialogueNode.nextNode = newNodeView.dialogueNode;
//                 }
//                 EditorUtility.SetDirty(editor.selectedDialogue);
//             }
//             else
//             {
//                 edge.input = null;
//                 edge.output = null;
//                 edge.RemoveFromHierarchy();
//             }
//         }

//         public void OnDrop(GraphView graphView, Edge edge)
//         {
//             // Standard connection handling
//         }
//     }

//     /// <summary>
//     /// Visual representation of a DialogueNode in the GraphView.
//     /// </summary>
//     public class DialogueNodeView : Node
//     {
//         public DialogueNode dialogueNode;
//         private readonly Dialogue selectedDialogue;
//         private readonly List<VisualElement> choiceElements = new List<VisualElement>();

//         public DialogueNodeView(DialogueNode node, Dialogue dialogue)
//         {
//             dialogueNode = node;
//             selectedDialogue = dialogue;

//             // Input port
//             var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(DialogueNode));
//             inputPort.portName = "Input";
//             inputContainer.Add(inputPort);

//             // Output port for linear progression
//             var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueNode));
//             outputPort.portName = "Next";
//             outputContainer.Add(outputPort);

//             // Register for position changes to save
//             this.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

//             // Add context menu for node deletion
//             this.AddManipulator(new ContextualMenuManipulator(BuildNodeContextMenu));

//             // Node fields
//             var speakerField = new TextField("Speaker") { value = dialogueNode.speakerName };
//             speakerField.RegisterValueChangedCallback(evt =>
//             {
//                 dialogueNode.speakerName = evt.newValue;
//                 title = string.IsNullOrEmpty(evt.newValue) ? "Node" : evt.newValue;
//                 EditorUtility.SetDirty(dialogueNode);
//             });
//             mainContainer.Add(speakerField);

//             var textField = new TextField("Dialogue Text") { value = dialogueNode.dialogueText, multiline = true };
//             textField.RegisterValueChangedCallback(evt =>
//             {
//                 dialogueNode.dialogueText = evt.newValue;
//                 EditorUtility.SetDirty(dialogueNode);
//             });
//             mainContainer.Add(textField);

//             // Start node toggle
//             var startToggle = new Toggle("Start Node") { value = dialogue.startNode == dialogueNode };
//             startToggle.RegisterValueChangedCallback(evt =>
//             {
//                 if (evt.newValue)
//                 {
//                     dialogue.startNode = dialogueNode;
//                 }
//                 else if (dialogue.startNode == dialogueNode)
//                 {
//                     dialogue.startNode = null;
//                 }
//                 EditorUtility.SetDirty(dialogue);
//             });
//             mainContainer.Add(startToggle);

//             var addChoiceButton = new Button(() => AddChoice()) { text = "Add Choice" };
//             mainContainer.Add(addChoiceButton);
//         }

//         // Override to handle selection: Show this node's SO in Inspector
//         public override void OnSelected()
//         {
//             base.OnSelected();
//             // Select the underlying DialogueNode SO in Unity's Selection
//             // This swaps the Inspector to show its properties (conditions, consequences, etc.)
//             Selection.activeObject = dialogueNode;
//             EditorGUIUtility.PingObject(dialogueNode); // Optional: Highlights in Project window
//         }

//         // Override to handle deselection: Fallback to Dialogue asset
//         public override void OnUnselected()
//         {
//             base.OnUnselected();
//             // Optional: Clear selection if deselected (or keep last node selected)
//             if (Selection.activeObject == dialogueNode)
//             {
//                 Selection.activeObject = selectedDialogue; // Fallback to the Dialogue asset
//             }
//         }

//         private void BuildNodeContextMenu(ContextualMenuPopulateEvent evt)
//         {
//             if (selectedDialogue.startNode != dialogueNode)
//             {
//                 evt.menu.AppendAction("Delete Node", a => DeleteNode());
//             }
//             else
//             {
//                 evt.menu.AppendAction("Delete Node", a => { }, DropdownMenuAction.Status.Disabled);
//             }
//         }

//         public void DeleteNode()
//         {
//             if (selectedDialogue == null || dialogueNode == null) return;
//             if (selectedDialogue.startNode == dialogueNode)
//             {
//                 Debug.LogWarning("Cannot delete the start node.");
//                 return;
//             }

//             // Disconnect all input and output connections
//             var inputPort = inputContainer.Q<Port>();
//             if (inputPort != null)
//             {
//                 var inputConnections = inputPort.connections.ToList();
//                 foreach (var edge in inputConnections)
//                 {
//                     edge.input.Disconnect(edge);
//                     edge.output.Disconnect(edge);
//                     edge.RemoveFromHierarchy();
//                 }
//             }

//             foreach (var port in outputContainer.Children().OfType<Port>())
//             {
//                 var outputConnections = port.connections.ToList();
//                 foreach (var edge in outputConnections)
//                 {
//                     edge.input.Disconnect(edge);
//                     edge.output.Disconnect(edge);
//                     edge.RemoveFromHierarchy();
//                 }
//             }

//             // Clear references to this node in other nodes
//             foreach (var otherNode in selectedDialogue.nodes)
//             {
//                 if (otherNode == dialogueNode) continue;
//                 if (otherNode.nextNode == dialogueNode)
//                 {
//                     otherNode.nextNode = null;
//                     EditorUtility.SetDirty(otherNode);
//                 }
//                 for (int i = 0; i < otherNode.choices.Count; i++)
//                 {
//                     if (otherNode.choices[i].targetNode == dialogueNode)
//                     {
//                         otherNode.choices[i].targetNode = null;
//                         EditorUtility.SetDirty(otherNode);
//                     }
//                 }
//             }

//             // Remove node position
//             selectedDialogue.nodePositions.RemoveAll(np => np.nodeId == dialogueNode.nodeId);

//             // Remove node from dialogue and graph
//             selectedDialogue.nodes.Remove(dialogueNode);
//             this.RemoveFromHierarchy();

//             // Delete the node asset
//             AssetDatabase.RemoveObjectFromAsset(dialogueNode);
//             EditorUtility.SetDirty(selectedDialogue);
//             AssetDatabase.SaveAssets();

//             // Destroy the node object (Unity API call)
//             if (dialogueNode != null)
//             {
//                 UnityEngine.Object.DestroyImmediate(dialogueNode);
//             }
//         }

//         private void OnGeometryChanged(GeometryChangedEvent evt)
//         {
//             if (selectedDialogue != null && dialogueNode != null)
//             {
//                 selectedDialogue.SetNodePosition(dialogueNode.nodeId, GetPosition());
//                 EditorUtility.SetDirty(selectedDialogue);
//             }
//         }

//         private void AddChoice()
//         {
//             if (dialogueNode == null) return;
//             dialogueNode.choices.Add(new DialogueChoice { choiceText = "New Choice" });
//             RefreshChoices();
//             EditorUtility.SetDirty(dialogueNode);
//         }

//         public void RefreshChoices()
//         {
//             // Remove old choice elements and ports
//             foreach (var element in choiceElements)
//             {
//                 mainContainer.Remove(element);
//             }
//             choiceElements.Clear();

//             // Remove existing choice ports (keep the "Next" port at index 0)
//             while (outputContainer.childCount > 1)
//             {
//                 outputContainer.RemoveAt(outputContainer.childCount - 1);
//             }

//             // Add choice fields and ports
//             for (int i = 0; i < dialogueNode.choices.Count; i++)
//             {
//                 var choice = dialogueNode.choices[i];
//                 var choiceContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };

//                 var choiceField = new TextField("Choice") { value = choice.choiceText };
//                 choiceField.RegisterValueChangedCallback(evt =>
//                 {
//                     choice.choiceText = evt.newValue;
//                     EditorUtility.SetDirty(dialogueNode);
//                 });
//                 choiceContainer.Add(choiceField);

//                 int currentIndex = i; // Local variable to capture the correct index in the lambda
//                 var deleteButton = new Button(() => DeleteChoice(currentIndex)) { text = "X" };
//                 choiceContainer.Add(deleteButton);

//                 mainContainer.Add(choiceContainer);
//                 choiceElements.Add(choiceContainer);

//                 var choicePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(DialogueNode));
//                 choicePort.portName = $"Choice {i + 1}";
//                 choicePort.userData = i; // Store choice index
//                 outputContainer.Add(choicePort);
//             }
//         }

//         private void DeleteChoice(int index)
//         {
//             Debug.Log("Delete attempted at index " + index);
//             if (index < 0 || index >= dialogueNode.choices.Count) return;

//             // Clear any connections for the choice's port
//             var choicePort = outputContainer[index + 1].Q<Port>();
//             if (choicePort != null)
//             {
//                 var connections = choicePort.connections.ToList();
//                 foreach (var edge in connections)
//                 {
//                     edge.input.Disconnect(edge);
//                     edge.output.Disconnect(edge);
//                     edge.RemoveFromHierarchy();
//                 }
//             }

//             // Remove the choice from the data model
//             dialogueNode.choices.RemoveAt(index);

//             // Refresh the UI and ports
//             RefreshChoices();

//             // Mark the node and dialogue as dirty to save changes
//             EditorUtility.SetDirty(dialogueNode);
//             EditorUtility.SetDirty(selectedDialogue);
//             AssetDatabase.SaveAssets();
//         }

//         public int GetOutputPortChoiceIndex(Port port)
//         {
//             if (port.userData is int index)
//             {
//                 return index;
//             }
//             return -1; // Not a choice port (main output)
//         }
//     }
// }

using UnityEditor;
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
            ConstructGraphView();
            AddToolbar();
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(graphView);
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

        private void AddToolbar()
        {
            var toolbar = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 5,
                    paddingBottom = 5,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f)
                }
            };

            var loadButton = new Button(() => LoadDialogue()) { text = "Load Dialogue" };
            var saveButton = new Button(() => SaveDialogue()) { text = "Save" };
            var previewButton = new Button(() => PreviewDialogue()) { text = "Preview" };

            toolbar.Add(loadButton);
            toolbar.Add(saveButton);
            toolbar.Add(previewButton);
            rootVisualElement.Add(toolbar);
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

            var nodeView = new DialogueNodeView(dialogueNode, selectedDialogue)
            {
                title = dialogueNode.speakerName != "" ? dialogueNode.speakerName : "Node"
            };
            nodeView.SetPosition(new Rect(finalPos, new Vector2(250, 300)));
            graphView.AddElement(nodeView);

            // Refresh choices display
            nodeView.RefreshChoices();

            return nodeView;
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change)
        {
            // Handle edge creation (links between nodes)
            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var outputNode = edge.output.node as DialogueNodeView;
                    var inputNode = edge.input.node as DialogueNodeView;
                    if (outputNode != null && inputNode != null)
                    {
                        // Check if this is a choice connection or nextNode
                        int choiceIndex = outputNode.GetOutputPortChoiceIndex(edge.output);
                        if (choiceIndex >= 0)
                        {
                            // Connect as a choice target
                            outputNode.dialogueNode.choices[choiceIndex].targetNode = inputNode.dialogueNode;
                        }
                        else
                        {
                            // Linear connection
                            outputNode.dialogueNode.nextNode = inputNode.dialogueNode;
                        }
                        EditorUtility.SetDirty(selectedDialogue);
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
            if (!string.IsNullOrEmpty(path))
            {
                path = path.Replace(Application.dataPath, "Assets");
                selectedDialogue = AssetDatabase.LoadAssetAtPath<Dialogue>(path);
                if (selectedDialogue != null)
                {
                    RebuildGraph();
                    // Auto-center view on nodes after load
                    FrameNodes();
                }
                else
                {
                    Debug.LogError("Selected file is not a Dialogue asset.");
                }
            }
        }

        private void RebuildGraph()
        {
            graphView.DeleteElements(graphView.graphElements.ToList());
            if (selectedDialogue == null) return;

            // Create node views with saved positions
            foreach (var node in selectedDialogue.nodes)
            {
                AddNodeToGraph(node, Vector2.zero);
            }

            // Reconnect edges
            foreach (var node in selectedDialogue.nodes)
            {
                var nodeView = graphView.nodes.ToList().OfType<DialogueNodeView>()
                    .FirstOrDefault(n => n.dialogueNode == node);
                if (nodeView == null) continue;

                // Linear connection
                if (node.nextNode != null)
                {
                    var targetView = graphView.nodes.ToList().OfType<DialogueNodeView>()
                        .FirstOrDefault(n => n.dialogueNode == node.nextNode);
                    if (targetView != null)
                    {
                        var edge = nodeView.outputContainer[0].Q<Port>().ConnectTo(targetView.inputContainer[0].Q<Port>());
                        graphView.AddElement(edge);
                    }
                }

                // Choice connections
                for (int i = 0; i < node.choices.Count; i++)
                {
                    var choice = node.choices[i];
                    if (choice.targetNode != null)
                    {
                        var targetView = graphView.nodes.ToList().OfType<DialogueNodeView>()
                            .FirstOrDefault(n => n.dialogueNode == choice.targetNode);
                        if (targetView != null)
                        {
                            var choicePort = nodeView.outputContainer[i + 1].Q<Port>();
                            var edge = choicePort.ConnectTo(targetView.inputContainer[0].Q<Port>());
                            graphView.AddElement(edge);
                        }
                    }
                }
            }
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
            if (selectedDialogue != null)
            {
                EditorUtility.SetDirty(selectedDialogue);
                AssetDatabase.SaveAssets();
                Debug.Log("Dialogue saved.");
            }
            else
            {
                Debug.LogWarning("No dialogue selected to save.");
            }
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
                            int choiceIndex = outputNode.GetOutputPortChoiceIndex(edge.output);
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
                int choiceIndex = outputNode.GetOutputPortChoiceIndex(edge.output);
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
            var speakerField = new TextField("Speaker") { value = dialogueNode.speakerName };
            speakerField.RegisterValueChangedCallback(evt =>
            {
                dialogueNode.speakerName = evt.newValue;
                title = string.IsNullOrEmpty(evt.newValue) ? "Node" : evt.newValue;
                EditorUtility.SetDirty(dialogueNode);
            });
            mainContainer.Add(speakerField);

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

        public int GetOutputPortChoiceIndex(Port port)
        {
            if (port.userData is int index)
            {
                return index;
            }
            return -1; // Not a choice port (main output)
        }
    }
}