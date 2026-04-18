#if MOSAIC_HAS_VISUALSCRIPTING
using UnityEngine;
using UnityEditor;
using Unity.VisualScripting;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.VisualScripting
{
    public static class VisualScriptingCreateGraphTool
    {
        [MosaicTool("visualscripting/create_graph",
                    "Create a new Script Graph asset and optionally attach it to a GameObject via a ScriptMachine component",
                    isReadOnly: false,
                    category: "visualscripting")]
        public static ToolResult<VisualScriptingCreateGraphResult> Execute(VisualScriptingCreateGraphParams p)
        {
            // Validate path
            if (!p.Path.StartsWith("Assets/"))
                return ToolResult<VisualScriptingCreateGraphResult>.Fail(
                    "Path must start with 'Assets/'.",
                    ErrorCodes.INVALID_PARAM);

            if (!p.Path.EndsWith(".asset"))
                return ToolResult<VisualScriptingCreateGraphResult>.Fail(
                    "Path must end with '.asset'.",
                    ErrorCodes.INVALID_PARAM);

            // Ensure parent directory exists
            var dir = System.IO.Path.GetDirectoryName(p.Path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Create folders recursively
                var parts = dir.Replace("\\", "/").Split('/');
                var current = parts[0]; // "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            // Create the ScriptGraphAsset
            var graphAsset = ScriptableObject.CreateInstance<ScriptGraphAsset>();
            graphAsset.name = p.Name;

            AssetDatabase.CreateAsset(graphAsset, p.Path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var result = new VisualScriptingCreateGraphResult
            {
                AssetPath = p.Path,
                Name = p.Name
            };

            // Optionally attach to a GameObject
            if (!string.IsNullOrEmpty(p.AttachTo))
            {
                var go = GameObject.Find(p.AttachTo);
                if (go == null)
                {
                    // Graph was created, just can't attach
                    return ToolResult<VisualScriptingCreateGraphResult>.Fail(
                        $"Graph created at '{p.Path}' but target GameObject '{p.AttachTo}' not found for attachment.",
                        ErrorCodes.NOT_FOUND);
                }

                Undo.RecordObject(go, "Mosaic: Visual Scripting Attach Graph");
                var machine = go.GetComponent<ScriptMachine>();
                if (machine == null)
                    machine = Undo.AddComponent<ScriptMachine>(go);

                machine.nest.source = GraphSource.Macro;
                machine.nest.macro = graphAsset;

                result.AttachedTo = go.name;
                result.AttachedInstanceId = go.GetInstanceID();
            }

            return ToolResult<VisualScriptingCreateGraphResult>.Ok(result);
        }
    }
}
#endif
