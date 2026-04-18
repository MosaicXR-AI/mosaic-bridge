using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>Per-target sightline result.</summary>
    public sealed class SightlineHit
    {
        public float[] TargetPosition { get; set; }
        public bool IsVisible { get; set; }
        public float Distance { get; set; }
        public string BlockedBy { get; set; }
    }

    /// <summary>Result for analysis/sightline. Story 33-8.</summary>
    public sealed class AnalysisSightlineResult
    {
        public string Mode { get; set; }
        public int TotalTargets { get; set; }
        public int VisibleCount { get; set; }
        public int BlockedCount { get; set; }
        public List<SightlineHit> Results { get; set; } = new List<SightlineHit>();
        public float ViewshedPercent { get; set; }
        public int AnnotationId { get; set; } = -1;
    }
}
