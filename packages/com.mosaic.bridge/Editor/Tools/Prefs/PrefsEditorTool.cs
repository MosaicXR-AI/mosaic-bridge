using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefs
{
    public static class PrefsEditorTool
    {
        [MosaicTool("prefs/editor",
                    "Gets, sets, or deletes an EditorPrefs key (string values)",
                    isReadOnly: false)]
        public static ToolResult<PrefsEditorResult> EditorPrefsAction(PrefsEditorParams p)
        {
            var action = p.Action?.Trim().ToLowerInvariant();

            switch (action)
            {
                case "get":
                {
                    bool exists = EditorPrefs.HasKey(p.Key);
                    string val = exists ? EditorPrefs.GetString(p.Key) : null;
                    return ToolResult<PrefsEditorResult>.Ok(new PrefsEditorResult
                    {
                        Action  = "get",
                        Key     = p.Key,
                        Value   = val,
                        Existed = exists
                    });
                }
                case "set":
                {
                    bool existed = EditorPrefs.HasKey(p.Key);
                    EditorPrefs.SetString(p.Key, p.Value ?? "");
                    return ToolResult<PrefsEditorResult>.Ok(new PrefsEditorResult
                    {
                        Action  = "set",
                        Key     = p.Key,
                        Value   = p.Value ?? "",
                        Existed = existed
                    });
                }
                case "delete":
                {
                    bool existed = EditorPrefs.HasKey(p.Key);
                    if (existed) EditorPrefs.DeleteKey(p.Key);
                    return ToolResult<PrefsEditorResult>.Ok(new PrefsEditorResult
                    {
                        Action  = "delete",
                        Key     = p.Key,
                        Existed = existed
                    });
                }
                default:
                    return ToolResult<PrefsEditorResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: get, set, delete",
                        ErrorCodes.INVALID_PARAM);
            }
        }
    }
}
