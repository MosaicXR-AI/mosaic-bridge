using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiUtilityEvaluateResult
    {
        public string              SelectedAction { get; set; }
        public List<ActionScore>   Scores         { get; set; }
    }

    public sealed class ActionScore
    {
        public string                    Action         { get; set; }
        public float                     Score          { get; set; }
        public List<ConsiderationScore>  Considerations { get; set; }
    }

    public sealed class ConsiderationScore
    {
        public string Input      { get; set; }
        public float  RawValue   { get; set; }
        public float  CurveOutput { get; set; }
    }
}
