namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Params for analysis/sightline. Story 33-8.
    /// </summary>
    public sealed class AnalysisSightlineParams
    {
        /// <summary>Observer location [x,y,z]. Required unless ViewerGameObject is provided.</summary>
        public float[] ViewerPosition { get; set; }

        /// <summary>GameObject name to use as the observer (alternative to ViewerPosition).</summary>
        public string ViewerGameObject { get; set; }

        /// <summary>Mode: "sightline" (default), "viewshed", "cone".</summary>
        public string Mode { get; set; } = "sightline";

        /// <summary>List of target positions [[x,y,z], ...] to test visibility (sightline mode).</summary>
        public float[][] Targets { get; set; }

        /// <summary>List of GameObject names to test visibility (sightline mode).</summary>
        public string[] TargetGameObjects { get; set; }

        /// <summary>Maximum ray distance. Default 100.</summary>
        public float MaxDistance { get; set; } = 100f;

        /// <summary>Cone field-of-view in degrees (cone mode). Default 60.</summary>
        public float FieldOfView { get; set; } = 60f;

        /// <summary>Look direction [x,y,z] for cone mode. Default forward [0,0,1].</summary>
        public float[] LookDirection { get; set; }

        /// <summary>Rays per hemisphere for viewshed mode. Default 32.</summary>
        public int ViewshedResolution { get; set; } = 32;

        /// <summary>Layer mask — which layers block sight. Default -1 (all).</summary>
        public int LayerMask { get; set; } = -1;

        /// <summary>If true, draws LineRenderer rays (green=visible, red=blocked).</summary>
        public bool CreateVisual { get; set; } = false;

        /// <summary>Visible ray color [r,g,b,a]. Default green [0,1,0,1].</summary>
        public float[] VisibleColor { get; set; }

        /// <summary>Blocked ray color [r,g,b,a]. Default red [1,0,0,1].</summary>
        public float[] BlockedColor { get; set; }
    }
}
