#if MOSAIC_HAS_TMP
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public static class TmpInfoTool
    {
        [MosaicTool("tmp/info",
                    "Queries TextMeshPro components in the scene — returns text, font, size, color, alignment, style, bounds, character count, and overflow mode",
                    isReadOnly: true)]
        public static ToolResult<TmpInfoResult> Execute(TmpInfoParams p)
        {
            var infos = new List<TmpComponentInfo>();

            if (!string.IsNullOrEmpty(p.GameObjectName))
            {
                var go = GameObject.Find(p.GameObjectName);
                if (go == null)
                    return ToolResult<TmpInfoResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found.",
                        ErrorCodes.NOT_FOUND);

                var tmp = go.GetComponent<TMP_Text>();
                if (tmp == null)
                    return ToolResult<TmpInfoResult>.Fail(
                        $"No TextMeshPro component found on '{p.GameObjectName}'.",
                        ErrorCodes.NOT_FOUND);

                infos.Add(BuildInfo(tmp));
            }
            else
            {
                // Find all TMP_Text components in the scene
#if UNITY_2023_1_OR_NEWER
                var all = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
#else
                var all = Object.FindObjectsOfType<TMP_Text>();
#endif
                foreach (var tmp in all)
                    infos.Add(BuildInfo(tmp));
            }

            return ToolResult<TmpInfoResult>.Ok(new TmpInfoResult
            {
                Components = infos.ToArray()
            });
        }

        private static TmpComponentInfo BuildInfo(TMP_Text tmp)
        {
            var bounds = tmp.bounds;
            string componentType = tmp is TextMeshProUGUI ? "TextMeshProUGUI" : "TextMeshPro";

            return new TmpComponentInfo
            {
                GameObjectName = tmp.gameObject.name,
                InstanceId     = tmp.gameObject.GetInstanceID(),
                HierarchyPath  = TmpToolHelpers.GetHierarchyPath(tmp.transform),
                ComponentType  = componentType,
                Text           = tmp.text,
                FontName       = tmp.font != null ? tmp.font.name : null,
                FontSize       = tmp.fontSize,
                Color          = new[] { tmp.color.r, tmp.color.g, tmp.color.b, tmp.color.a },
                Alignment      = tmp.alignment.ToString(),
                FontStyle      = tmp.fontStyle.ToString(),
                OverflowMode   = tmp.overflowMode.ToString(),
                Bounds         = new[] { bounds.center.x, bounds.center.y, bounds.center.z,
                                         bounds.size.x, bounds.size.y, bounds.size.z },
                CharacterCount = tmp.textInfo != null ? tmp.textInfo.characterCount : 0
            };
        }
    }
}
#endif
