using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenLSystemParams
    {
        /// <summary>Starting axiom string. Default "F".</summary>
        public string              Axiom        { get; set; }

        /// <summary>Production rules for symbol replacement.</summary>
        public List<LSystemRule>   Rules        { get; set; }

        /// <summary>Number of expansion iterations (clamped 1-8). Default 4.</summary>
        public int?                Iterations   { get; set; }

        /// <summary>Turning angle in degrees. Default 25.</summary>
        public float?              Angle        { get; set; }

        /// <summary>Forward step length. Default 1.0.</summary>
        public float?              StepLength   { get; set; }

        /// <summary>Step length multiplier per iteration depth. Default 0.8.</summary>
        public float?              LengthDecay  { get; set; }

        /// <summary>Branch thickness decay at each push. Default 0.7.</summary>
        public float?              RadiusDecay  { get; set; }

        /// <summary>Initial branch radius. Default 0.1.</summary>
        public float?              InitialRadius { get; set; }

        /// <summary>Preset name: "tree", "fern", "bush", "coral", "vine". Overrides axiom/rules.</summary>
        public string              Preset       { get; set; }

        /// <summary>Whether to generate a mesh GameObject. Default true.</summary>
        public bool?               GenerateMesh { get; set; }

        /// <summary>Parent GameObject name to parent the result under.</summary>
        public string              ParentObject { get; set; }

        /// <summary>World position [x, y, z].</summary>
        public float[]             Position     { get; set; }

        /// <summary>Random seed for stochastic rules. Random if null.</summary>
        public int?                Seed         { get; set; }

        /// <summary>Name for the generated GameObject. Default "LSystem".</summary>
        public string              Name         { get; set; }
    }

    public sealed class LSystemRule
    {
        /// <summary>Single character symbol to match.</summary>
        public string Symbol      { get; set; }

        /// <summary>Replacement string for this symbol.</summary>
        public string Replacement { get; set; }

        /// <summary>Probability of applying this rule (0-1). Default 1.0 for deterministic.</summary>
        public float? Probability { get; set; }
    }
}
