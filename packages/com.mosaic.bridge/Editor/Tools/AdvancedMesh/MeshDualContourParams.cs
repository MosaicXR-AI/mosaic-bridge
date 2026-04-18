namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    /// <summary>
    /// Parameters for mesh/dual-contour. Generates a mesh from a built-in SDF (Signed Distance Function)
    /// using the Dual Contouring algorithm (Ju, Losasso, Schaefer, Warren 2002).
    /// </summary>
    public sealed class MeshDualContourParams
    {
        /// <summary>Built-in SDF function: "sphere", "box", "torus", or "noise".</summary>
        public string SdfFunction { get; set; } = "sphere";

        /// <summary>Radius parameter for sphere/torus SDFs.</summary>
        public float SdfRadius { get; set; } = 5f;

        /// <summary>Box half-extents [x, y, z] for box SDF.</summary>
        public float[] SdfSize { get; set; }

        /// <summary>Voxel grid resolution per axis. Clamped to [4, 64] for safety.</summary>
        public int Resolution { get; set; } = 32;

        /// <summary>Bounding box minimum corner [x, y, z].</summary>
        public float[] BoundsMin { get; set; }

        /// <summary>Bounding box maximum corner [x, y, z].</summary>
        public float[] BoundsMax { get; set; }

        /// <summary>Iso-value of the SDF surface to extract. Default 0 (the SDF surface itself).</summary>
        public float IsoValue { get; set; } = 0f;

        /// <summary>Sharp feature angle threshold in degrees (currently informational — MVP uses simple averaging).</summary>
        public float SharpFeatureAngle { get; set; } = 30f;

        /// <summary>If true, runs basic vertex welding to simplify output (MVP no-op).</summary>
        public bool Simplify { get; set; } = false;

        /// <summary>Output mesh asset name (without extension). Defaults to "DualContour_{Sdf}".</summary>
        public string OutputName { get; set; }

        /// <summary>Folder to place generated mesh asset (Assets-relative).</summary>
        public string SavePath { get; set; } = "Assets/Generated/Mesh/";
    }
}
