using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Selection
{
    public static class SelectionFocusSceneViewTool
    {
        [MosaicTool("selection/focus-scene-view",
                    "Frames the current selection in the active Scene View and focuses the window",
                    isReadOnly: false)]
        public static ToolResult<SelectionFocusSceneViewResult> FocusSceneView(SelectionFocusSceneViewParams p)
        {
            if (SceneView.lastActiveSceneView == null)
            {
                return ToolResult<SelectionFocusSceneViewResult>.Ok(new SelectionFocusSceneViewResult
                {
                    Focused = false,
                    Message = "No active scene view"
                });
            }

            bool framed = SceneView.FrameLastActiveSceneView();
            SceneView.lastActiveSceneView?.Focus();

            return ToolResult<SelectionFocusSceneViewResult>.Ok(new SelectionFocusSceneViewResult
            {
                Focused = framed,
                Message = framed ? "Scene view framed" : "No active scene view"
            });
        }
    }
}
