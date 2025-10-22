using System;
using System.IO;
using System.Text;
using UnityEditor;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueSystem.Editor.Tools
{
    public static class DialogueEditorUIDump
    {
        [MenuItem("Tools/Dialogue Editor/Export UI Toolkit Dump")]
        public static void ExportUIToolkitDump()
        {
            // Find open DialogueEditor windows
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            EditorWindow target = null;
            foreach (var w in windows)
            {
                if (w.GetType().Name == "DialogueEditor")
                {
                    target = w;
                    break;
                }
            }

            if (target == null)
            {
                Debug.LogError("DialogueEditor window not found. Open the Dialogue Editor window and try again.");
                return;
            }

            var root = target.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("DialogueEditor.rootVisualElement is null.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Dialogue Editor UI Toolkit Dump");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("u"));
            sb.AppendLine("Window type: " + target.GetType().FullName);
            sb.AppendLine();

            try
            {
                DumpElement(root, sb, 0);
            }
            catch (Exception ex)
            {
                sb.AppendLine("Exception while dumping tree: " + ex.Message);
                sb.AppendLine(ex.StackTrace);
            }

            var outPath = Path.Combine(Application.dataPath, "../DialogueEditor_UI_Dump.txt");
            try
            {
                File.WriteAllText(outPath, sb.ToString());
                Debug.Log("UI Toolkit dump written to: " + outPath);
                EditorUtility.RevealInFinder(outPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to write UI dump: " + ex.Message);
            }
        }

        private static void DumpElement(VisualElement ve, StringBuilder sb, int depth)
        {
            var indent = new string(' ', depth * 2);
            sb.Append(indent);
            sb.Append("- ");
            sb.Append(ve.GetType().Name);
            if (!string.IsNullOrEmpty(ve.name)) sb.AppendFormat(" (name: '{0}')", ve.name);
            var classes = ve.GetClasses();
            if (classes.Any()) sb.AppendFormat(" classes=[{0}]", string.Join(",", classes.ToArray()));

            // Try to get text property for controls like Label/Foldout
            try
            {
                var foldout = ve as Foldout;
                if (foldout != null)
                {
                    sb.AppendFormat(" text=\"{0}\"", foldout.text);
                }
                else
                {
                    // attempt reflection for a 'text' property
                    var prop = ve.GetType().GetProperty("text");
                    if (prop != null)
                    {
                        var val = prop.GetValue(ve, null);
                        if (val != null) sb.AppendFormat(" text=\"{0}\"", val.ToString());
                    }
                }
            }
            catch { }

            // Layout and world bounds
            try
            {
                var r = ve.layout; // local layout
                var wb = ve.worldBound; // world bound
                sb.AppendFormat(" layout=[{0},{1},{2},{3}]", r.x, r.y, r.width, r.height);
                sb.AppendFormat(" world=[{0},{1},{2},{3}]", wb.x, wb.y, wb.width, wb.height);
            }
            catch { }

            // Resolved style snapshot
            try
            {
                var rs = ve.resolvedStyle;
                sb.AppendFormat(" display={0} visibility={1} opacity={2}", rs.display, rs.visibility, rs.opacity);
                sb.AppendFormat(" width={0} height={1} left={2} top={3}", rs.width, rs.height, rs.left, rs.top);
            }
            catch { }

            // other useful flags
            sb.AppendFormat(" pickingMode={0}", ve.pickingMode);
            sb.AppendFormat(" enabledInHierarchy={0}", ve.enabledInHierarchy);
            sb.AppendLine();

            // children
            foreach (var child in ve.hierarchy.Children())
            {
                try
                {
                    DumpElement(child, sb, depth + 1);
                }
                catch (Exception ex)
                {
                    sb.AppendLine(indent + "  (exception reading child) " + ex.Message);
                }
            }
        }
    }
}
