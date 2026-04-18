using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefs
{
    public static class PrefsPlayerTool
    {
        [MosaicTool("prefs/player",
                    "Gets, sets, deletes, or deletes all PlayerPrefs keys (string values)",
                    isReadOnly: false)]
        public static ToolResult<PrefsPlayerResult> PlayerPrefsAction(PrefsPlayerParams p)
        {
            var action = p.Action?.Trim().ToLowerInvariant();

            switch (action)
            {
                case "get":
                {
                    if (string.IsNullOrEmpty(p.Key))
                        return ToolResult<PrefsPlayerResult>.Fail(
                            "Key is required for get action", ErrorCodes.INVALID_PARAM);

                    bool exists = PlayerPrefs.HasKey(p.Key);
                    string val = exists ? PlayerPrefs.GetString(p.Key) : null;
                    return ToolResult<PrefsPlayerResult>.Ok(new PrefsPlayerResult
                    {
                        Action  = "get",
                        Key     = p.Key,
                        Value   = val,
                        Existed = exists
                    });
                }
                case "set":
                {
                    if (string.IsNullOrEmpty(p.Key))
                        return ToolResult<PrefsPlayerResult>.Fail(
                            "Key is required for set action", ErrorCodes.INVALID_PARAM);

                    bool existed = PlayerPrefs.HasKey(p.Key);
                    PlayerPrefs.SetString(p.Key, p.Value ?? "");
                    PlayerPrefs.Save();
                    return ToolResult<PrefsPlayerResult>.Ok(new PrefsPlayerResult
                    {
                        Action  = "set",
                        Key     = p.Key,
                        Value   = p.Value ?? "",
                        Existed = existed
                    });
                }
                case "delete":
                {
                    if (string.IsNullOrEmpty(p.Key))
                        return ToolResult<PrefsPlayerResult>.Fail(
                            "Key is required for delete action", ErrorCodes.INVALID_PARAM);

                    bool existed = PlayerPrefs.HasKey(p.Key);
                    if (existed) PlayerPrefs.DeleteKey(p.Key);
                    PlayerPrefs.Save();
                    return ToolResult<PrefsPlayerResult>.Ok(new PrefsPlayerResult
                    {
                        Action  = "delete",
                        Key     = p.Key,
                        Existed = existed
                    });
                }
                case "delete-all":
                {
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    return ToolResult<PrefsPlayerResult>.Ok(new PrefsPlayerResult
                    {
                        Action = "delete-all"
                    });
                }
                default:
                    return ToolResult<PrefsPlayerResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: get, set, delete, delete-all",
                        ErrorCodes.INVALID_PARAM);
            }
        }
    }
}
