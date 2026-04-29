using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    /// <summary>
    /// Registry of common ShaderGraph node types with their display names,
    /// fully-qualified type strings, and slot definitions.
    /// Slot IDs match Unity ShaderGraph 14.x–17.x (Unity 2022 LTS – Unity 6).
    /// </summary>
    internal static class ShaderGraphNodeRegistry
    {
        internal sealed class SlotDef
        {
            internal int    Id              { get; set; }
            internal string DisplayName     { get; set; }
            internal int    SlotType        { get; set; }   // 0=Input 1=Output
            internal int    StageCapability { get; set; } = 3;  // 1=Vertex 2=Fragment 3=Both
        }

        internal sealed class NodeDef
        {
            internal string     TypeName    { get; set; }  // fully-qualified C# type
            internal string     DisplayName { get; set; }  // shown in node header
            internal SlotDef[]  Slots       { get; set; }
            internal int        SGVersion   { get; set; }  // m_SGVersion in JSON (default 0)
            internal string     ExtraFields { get; set; }  // additional JSON fields for this node type
        }

        // Friendly alias → NodeDef
        private static readonly Dictionary<string, NodeDef> _registry =
            new Dictionary<string, NodeDef>(System.StringComparer.OrdinalIgnoreCase)
        {
            // ── Math ─────────────────────────────────────────────────────────
            ["add"]        = new NodeDef { TypeName = "UnityEditor.ShaderGraph.AddNode",      DisplayName = "Add",
                Slots = new[]{ S(0,"A",0), S(1,"B",0), S(2,"Out",1) } },
            ["subtract"]   = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SubtractNode", DisplayName = "Subtract",
                Slots = new[]{ S(0,"A",0), S(1,"B",0), S(2,"Out",1) } },
            ["multiply"]   = new NodeDef { TypeName = "UnityEditor.ShaderGraph.MultiplyNode", DisplayName = "Multiply",
                Slots = new[]{ S(0,"A",0), S(1,"B",0), S(2,"Out",1) } },
            ["divide"]     = new NodeDef { TypeName = "UnityEditor.ShaderGraph.DivideNode",   DisplayName = "Divide",
                Slots = new[]{ S(0,"A",0), S(1,"B",0), S(2,"Out",1) } },
            ["power"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.PowerNode",    DisplayName = "Power",
                Slots = new[]{ S(0,"Base",0), S(1,"Exp",0), S(2,"Out",1) } },
            ["lerp"]       = new NodeDef { TypeName = "UnityEditor.ShaderGraph.LerpNode",     DisplayName = "Lerp",
                Slots = new[]{ S(0,"A",0), S(1,"B",0), S(2,"T",0), S(3,"Out",1) } },
            ["clamp"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.ClampNode",    DisplayName = "Clamp",
                Slots = new[]{ S(0,"In",0), S(1,"Min",0), S(2,"Max",0), S(3,"Out",1) } },
            ["saturate"]   = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SaturateNode", DisplayName = "Saturate",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["abs"]        = new NodeDef { TypeName = "UnityEditor.ShaderGraph.AbsoluteNode", DisplayName = "Absolute",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["negate"]     = new NodeDef { TypeName = "UnityEditor.ShaderGraph.NegateNode",   DisplayName = "Negate",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["sqrt"]       = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SquareRootNode", DisplayName = "Square Root",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["floor"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.FloorNode",    DisplayName = "Floor",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["ceil"]       = new NodeDef { TypeName = "UnityEditor.ShaderGraph.CeilingNode",  DisplayName = "Ceiling",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["frac"]       = new NodeDef { TypeName = "UnityEditor.ShaderGraph.FractionNode", DisplayName = "Fraction",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["step"]       = new NodeDef { TypeName = "UnityEditor.ShaderGraph.StepNode",     DisplayName = "Step",
                Slots = new[]{ S(0,"Edge",0), S(1,"In",0), S(2,"Out",1) } },
            ["smoothstep"] = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SmoothstepNode", DisplayName = "Smoothstep",
                Slots = new[]{ S(0,"Edge1",0), S(1,"Edge2",0), S(2,"In",0), S(3,"Out",1) } },
            ["remap"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.RemapNode",    DisplayName = "Remap",
                Slots = new[]{ S(0,"In",0), S(1,"InMin",0), S(2,"InMax",0), S(3,"OutMin",0), S(4,"OutMax",0), S(5,"Out",1) } },
            ["normalblend"]= new NodeDef { TypeName = "UnityEditor.ShaderGraph.NormalBlendNode", DisplayName = "Normal Blend",
                Slots = new[]{ S(0,"A",0), S(1,"B",0), S(2,"Out",1) } },

            // ── Utility ───────────────────────────────────────────────────────
            ["split"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SplitNode",   DisplayName = "Split",
                Slots = new[]{ S(0,"In",0), S(1,"R",1), S(2,"G",1), S(3,"B",1), S(4,"A",1) } },
            ["combine"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.CombineNode", DisplayName = "Combine",
                Slots = new[]{ S(0,"R",0), S(1,"G",0), S(2,"B",0), S(3,"A",0), S(4,"RGBA",1), S(5,"RGB",1), S(6,"RG",1) } },
            ["swizzle"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SwizzleNode", DisplayName = "Swizzle",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["fresnel"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.FresnelEffectNode", DisplayName = "Fresnel Effect",
                Slots = new[]{ S(0,"Normal",0), S(1,"View Direction",0), S(2,"Power",0), S(3,"Out",1) } },

            // ── Input / Constant ──────────────────────────────────────────────
            ["float"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.Vector1Node",  DisplayName = "Float",
                Slots = new[]{ S(0,"Out",1) } },
            ["vector2"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.Vector2Node",  DisplayName = "Vector 2",
                Slots = new[]{ S(0,"Out",1) } },
            ["vector3"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.Vector3Node",  DisplayName = "Vector 3",
                Slots = new[]{ S(0,"Out",1) } },
            ["vector4"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.Vector4Node",  DisplayName = "Vector 4",
                Slots = new[]{ S(0,"Out",1) } },
            ["color"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.ColorNode",    DisplayName = "Color",
                Slots = new[]{ S(0,"Out",1) } },
            ["uv"]         = new NodeDef { TypeName = "UnityEditor.ShaderGraph.UVNode",       DisplayName = "UV",
                Slots = new[]{ S(0,"Out",1) } },
            ["time"]       = new NodeDef { TypeName = "UnityEditor.ShaderGraph.TimeNode",     DisplayName = "Time",
                Slots = new[]{ S(0,"Time",1), S(1,"Sine Time",1), S(2,"Cosine Time",1), S(3,"Delta Time",1), S(4,"Smooth Delta",1) } },
            ["position"]   = new NodeDef { TypeName = "UnityEditor.ShaderGraph.PositionNode", DisplayName = "Position",
                Slots = new[]{ S(0,"Out",1) } },
            ["normal"]     = new NodeDef { TypeName = "UnityEditor.ShaderGraph.NormalVectorNode", DisplayName = "Normal Vector",
                Slots = new[]{ S(0,"Out",1) } },
            ["viewdir"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.ViewDirectionNode", DisplayName = "View Direction",
                Slots = new[]{ S(0,"Out",1) } },

            // ── Texture sampling ─────────────────────────────────────────────
            // Texture/Sampler inputs + all outputs are fragment-only (StageCapability=2).
            ["sampletexture2d"] = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SampleTexture2DNode", DisplayName = "Sample Texture 2D",
                Slots = new[]{ S(0,"Texture",0,2), S(1,"UV",0), S(2,"Sampler",0,2), S(4,"RGBA",1,2), S(5,"R",1,2), S(6,"G",1,2), S(7,"B",1,2), S(8,"A",1,2) } },
            ["sampletexture"]   = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SampleTexture2DNode", DisplayName = "Sample Texture 2D",
                Slots = new[]{ S(0,"Texture",0,2), S(1,"UV",0), S(2,"Sampler",0,2), S(4,"RGBA",1,2), S(5,"R",1,2), S(6,"G",1,2), S(7,"B",1,2), S(8,"A",1,2) } },
            ["samplecubemap"]   = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SampleCubemapNode", DisplayName = "Sample Cubemap",
                Slots = new[]{ S(0,"Cube",0,2), S(1,"Dir",0), S(2,"LOD",0), S(3,"Sampler",0,2), S(4,"Out",1,2) } },

            // ── Transform ────────────────────────────────────────────────────
            ["transformvector"] = new NodeDef { TypeName = "UnityEditor.ShaderGraph.TransformNode", DisplayName = "Transform",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },
            ["normalunpack"]    = new NodeDef { TypeName = "UnityEditor.ShaderGraph.NormalUnpackNode", DisplayName = "Normal Unpack",
                Slots = new[]{ S(0,"In",0), S(1,"Out",1) } },

            // ── Procedural / Noise ───────────────────────────────────────────
            // VoronoiNode requires SGVersion=1 and m_HashType in JSON output.
            // UV slot must be serialized as UVMaterialSlot — handled in ShaderGraphAddNodeTool.
            ["voronoi"]      = new NodeDef { TypeName = "UnityEditor.ShaderGraph.VoronoiNode", DisplayName = "Voronoi",
                SGVersion = 1,
                ExtraFields = "\"m_HashType\": 0",
                Slots = new[]{ S(0,"UV",0), S(1,"AngleOffset",0), S(2,"CellDensity",0), S(3,"Out",1), S(4,"Cells",1) } },
            ["simplenoise"]  = new NodeDef { TypeName = "UnityEditor.ShaderGraph.SimpleNoiseNode", DisplayName = "Simple Noise",
                Slots = new[]{ S(0,"UV",0), S(1,"Scale",0), S(2,"Out",1) } },
            ["gradientnoise"]= new NodeDef { TypeName = "UnityEditor.ShaderGraph.GradientNoiseNode", DisplayName = "Gradient Noise",
                Slots = new[]{ S(0,"UV",0), S(1,"Scale",0), S(2,"Out",1) } },

            // ── Custom Function ──────────────────────────────────────────────
            // CustomFunctionNode requires SGVersion=1, m_SourceType=1 for inline body.
            // Output slots need m_LiteralMode:false — handled in ShaderGraphAddNodeTool.
            ["customfunction"] = new NodeDef { TypeName = "UnityEditor.ShaderGraph.CustomFunctionNode", DisplayName = "Custom Function",
                SGVersion = 1,
                ExtraFields = "\"m_SourceType\": 1",
                Slots = new[]{ S(0,"Out",1) } },
        };

        internal static NodeDef Get(string alias)
        {
            _registry.TryGetValue(alias?.Trim(), out var def);
            return def;
        }

        internal static IEnumerable<string> AllAliases() => _registry.Keys;

        private static SlotDef S(int id, string name, int slotType, int stageCap = 3) =>
            new SlotDef { Id = id, DisplayName = name, SlotType = slotType, StageCapability = stageCap };
    }
}
