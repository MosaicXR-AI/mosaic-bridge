namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Result envelope for the data/heatmap tool (Story 34-1).
    /// </summary>
    public sealed class DataHeatmapResult
    {
        /// <summary>Asset path of the generated heatmap material.</summary>
        public string MaterialPath { get; set; }

        /// <summary>Asset path of the baked heatmap PNG texture.</summary>
        public string TexturePath { get; set; }

        /// <summary>Name of the target GameObject the heatmap was applied to.</summary>
        public string TargetObject { get; set; }

        /// <summary>Minimum value used for normalization.</summary>
        public float ValueMin { get; set; }

        /// <summary>Maximum value used for normalization.</summary>
        public float ValueMax { get; set; }

        /// <summary>Name of the child legend GameObject, or empty when ShowLegend is false.</summary>
        public string LegendGameObject { get; set; }
    }
}
