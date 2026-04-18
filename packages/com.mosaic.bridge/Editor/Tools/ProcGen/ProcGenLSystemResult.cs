namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenLSystemResult
    {
        /// <summary>The expanded L-system string (truncated at 1000 chars if longer).</summary>
        public string GeneratedString  { get; set; }

        /// <summary>Full length of the expanded string.</summary>
        public int    StringLength     { get; set; }

        /// <summary>Name of the created GameObject (null if GenerateMesh=false).</summary>
        public string GameObjectName   { get; set; }

        /// <summary>Instance ID of the created GameObject (0 if GenerateMesh=false).</summary>
        public int    InstanceId       { get; set; }

        /// <summary>Number of mesh vertices (0 if GenerateMesh=false).</summary>
        public int    VertexCount      { get; set; }

        /// <summary>Number of branch segments generated.</summary>
        public int    BranchCount      { get; set; }

        /// <summary>Number of iterations used.</summary>
        public int    Iterations       { get; set; }

        /// <summary>Preset name used (null if custom rules).</summary>
        public string Preset           { get; set; }
    }
}
