using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Measure
{
    public sealed class MeasureDistanceParams
    {
        /// <summary>Explicit point A [x,y,z]. Takes precedence over GameObjectA.</summary>
        public float[] PointA { get; set; }

        /// <summary>Explicit point B [x,y,z]. Takes precedence over GameObjectB.</summary>
        public float[] PointB { get; set; }

        /// <summary>GameObject name for point A (uses transform.position or surface).</summary>
        public string GameObjectA { get; set; }

        /// <summary>GameObject name for point B (uses transform.position or surface).</summary>
        public string GameObjectB { get; set; }

        /// <summary>Mode: "point_to_point" (default), "min_distance", "surface_to_surface".</summary>
        public string Mode { get; set; } = "point_to_point";

        /// <summary>Unit: "meters" (default), "feet", "inches", "millimeters", "centimeters".</summary>
        public string Unit { get; set; } = "meters";

        /// <summary>If true, creates a LineRenderer + TextMesh label annotation in the scene.</summary>
        public bool CreateVisual { get; set; } = false;

        /// <summary>Visual color [r,g,b,a]. Defaults to yellow [1,1,0,1].</summary>
        public float[] VisualColor { get; set; }

        /// <summary>LineRenderer width. Default 0.02.</summary>
        public float LineWidth { get; set; } = 0.02f;

        /// <summary>Optional annotation GameObject name.</summary>
        public string Name { get; set; }
    }
}
