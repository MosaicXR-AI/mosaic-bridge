using UnityEngine;

namespace Mosaic.Bridge.Core.Platform
{
    /// <summary>
    /// Cross-platform clipboard write service. All writes go through this class —
    /// no component calls GUIUtility.systemCopyBuffer directly.
    /// </summary>
    public static class ClipboardService
    {
        /// <summary>
        /// Writes <paramref name="text"/> to the system clipboard.
        /// Returns false (with a reason in <paramref name="error"/>) when the write is not possible.
        /// Never throws.
        /// </summary>
        public static bool TryWrite(string text, out string error)
        {
            // Clipboard is unavailable in batch mode (no display server)
            if (Application.isBatchMode)
            {
                error = "batch mode";
                return false;
            }

            try
            {
                GUIUtility.systemCopyBuffer = text ?? string.Empty;
                error = null;
                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
