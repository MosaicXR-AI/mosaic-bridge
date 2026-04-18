using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.EditorOps
{
    public sealed class EditorExecuteCodeParams
    {
        /// <summary>
        /// A static member access or method call expression, e.g.:
        ///   "UnityEngine.Application.dataPath"
        ///   "UnityEditor.AssetDatabase.Refresh()"
        ///   "UnityEngine.Screen.SetResolution(1920, 1080, false)"
        /// </summary>
        [Required] public string Code { get; set; }
    }
}
