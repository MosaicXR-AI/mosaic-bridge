namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Parameters for measure/volume. Requires a scene GameObject with a MeshFilter.
    /// Uses the signed-tetrahedron method; non-closed meshes return an approximation.
    /// </summary>
    public sealed class MeasureVolumeParams
    {
        /// <summary>Name of a scene GameObject with a MeshFilter. Required.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Volume unit: "m3" (default), "cm3", "ft3", "in3", "liters".</summary>
        public string Unit { get; set; } = "m3";

        /// <summary>If true, highlights the mesh in the scene view (selection-style).</summary>
        public bool CreateVisual { get; set; } = false;
    }
}
