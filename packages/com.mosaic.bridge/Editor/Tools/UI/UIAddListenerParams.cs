using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIAddListenerParams
    {
        // ── Source: the GameObject/Component carrying the UnityEvent ───────────
        public int? InstanceId { get; set; }
        public string GameObjectName { get; set; }

        /// <summary>Component type carrying the event, e.g. "Button", "Toggle", "Slider", or a
        /// custom MonoBehaviour name. Required.</summary>
        [Required] public string ComponentType { get; set; }

        /// <summary>The UnityEvent property or field name, e.g. "onClick" (Button),
        /// "onValueChanged" (Toggle/Slider). Required.</summary>
        [Required] public string EventName { get; set; }

        // ── Target: the GameObject/Component/method the listener calls ─────────
        public int? TargetInstanceId { get; set; }
        public string TargetGameObjectName { get; set; }

        /// <summary>Component type on the target carrying the method. Required.</summary>
        [Required] public string TargetComponentType { get; set; }

        /// <summary>Public method name, 0 or 1 parameters (float, int, bool, string, or an
        /// Object-derived type). Required.</summary>
        [Required] public string MethodName { get; set; }

        // ── Argument — exactly one, matching the method's own parameter type ────
        public float? FloatArg { get; set; }
        public int? IntArg { get; set; }
        public bool? BoolArg { get; set; }
        public string StringArg { get; set; }

        /// <summary>Asset path (optionally 'Assets/sheet.png#Sub', per O4 §3.1) for an
        /// Object-derived parameter.</summary>
        public string ObjectArg { get; set; }

        /// <summary>"RuntimeOnly" (default — matches AddPersistentListener's own programmatic
        /// default), "EditorAndRuntime", or "Off".</summary>
        public string CallState { get; set; }
    }
}
