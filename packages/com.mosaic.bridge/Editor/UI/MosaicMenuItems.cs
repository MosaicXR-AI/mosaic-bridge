using Mosaic.Bridge.Core.Diagnostics;
using Mosaic.Bridge.Core.Platform;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Top-level Mosaic menu items for end-user actions.
    /// Story diag-2-4: Mosaic > Report Issue.
    /// </summary>
    public static class MosaicMenuItems
    {
        [MenuItem("Mosaic/Report Issue", priority = 1)]
        public static void ReportIssue()
        {
            Execute();
        }

        /// <summary>
        /// Directly callable entry point — used by tests to avoid EditorApplication.ExecuteMenuItem.
        /// </summary>
        public static void Execute()
        {
            try
            {
                var assembler = new ReportAssembler();
                var report = assembler.BuildReport();

                if (ClipboardService.TryWrite(report, out _))
                {
                    Debug.Log("[Mosaic] Issue report copied to clipboard.");
                }
                else
                {
                    Debug.LogError("[Mosaic] Report Issue: clipboard write failed. See the report below.");
                    Debug.Log(report);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Mosaic] Report Issue failed: {ex.Message}");
            }
        }
    }
}
