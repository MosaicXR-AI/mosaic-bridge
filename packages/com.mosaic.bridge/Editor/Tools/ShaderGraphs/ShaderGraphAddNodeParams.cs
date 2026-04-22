namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphAddNodeParams
    {
        /// <summary>Asset path to the .shadergraph file (e.g. "Assets/Shaders/MyGraph.shadergraph").</summary>
        public string GraphPath { get; set; }

        /// <summary>
        /// Node type alias. Case-insensitive. Supported aliases:
        /// Math: add, subtract, multiply, divide, power, lerp, clamp, saturate, abs, negate, sqrt, floor, ceil, frac, step, smoothstep, remap, normalblend
        /// Utility: split, combine, swizzle, fresnel
        /// Input: float, vector2, vector3, vector4, color, uv, time, position, normal, viewdir
        /// Texture: sampletexture2d, samplecubemap
        /// Transform: transformvector, normalunpack
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>Optional display name for the node (shown in the graph header). Defaults to the node type's display name.</summary>
        public string NodeName { get; set; }

        /// <summary>Position of the node in the graph canvas [x, y]. Defaults to [0, 0].</summary>
        public float[] Position { get; set; }

        /// <summary>Optional initial float value for Float/Vector nodes.</summary>
        public float? DefaultValue { get; set; }
    }
}
