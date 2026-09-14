namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UICreateCanvasParams
    {
        /// <summary>Optional name for the Canvas GameObject. Defaults to "Canvas".</summary>
        public string Name { get; set; }

        /// <summary>Render mode: "Overlay", "Camera", or "WorldSpace". Defaults to "Overlay".</summary>
        public string RenderMode { get; set; }

        /// <summary>"auto" (default — matches whatever's installed: StandaloneInputModule unless
        /// the Input System package has registered its own override), "standalone" (force
        /// StandaloneInputModule, a no-op module in an Input System project — O4 L6), or
        /// "inputsystem" (force InputSystemUIInputModule; fails clearly if that package isn't
        /// installed rather than silently adding the wrong one). Ignored if an EventSystem
        /// already exists.</summary>
        public string InputModule { get; set; }

        // ── CanvasScaler ─────────────────────────────────────────────────────

        /// <summary>"ConstantPixelSize" (default), "ScaleWithScreenSize", or "ConstantPhysicalSize".</summary>
        public string ScaleMode { get; set; }

        /// <summary>[width, height]. Only meaningful with ScaleMode=ScaleWithScreenSize.</summary>
        public float[] ReferenceResolution { get; set; }

        /// <summary>0 (match width) .. 1 (match height). Only meaningful with ScaleMode=ScaleWithScreenSize.</summary>
        public float? MatchWidthOrHeight { get; set; }

        // ── Canvas ───────────────────────────────────────────────────────────

        public int? SortingOrder { get; set; }
        public bool? PixelPerfect { get; set; }

        /// <summary>Camera/WorldSpace only.</summary>
        public float? PlaneDistance { get; set; }

        /// <summary>Camera/WorldSpace: name of the camera GameObject. Camera/WorldSpace without
        /// this falls back to Camera.main.</summary>
        public string WorldCamera { get; set; }

        /// <summary>WorldSpace only: [width, height] world-space size of the canvas RectTransform.</summary>
        public float[] WorldSize { get; set; }

        /// <summary>Optional: reparent the new Canvas under this GameObject (by name).</summary>
        public string Parent { get; set; }
    }
}
