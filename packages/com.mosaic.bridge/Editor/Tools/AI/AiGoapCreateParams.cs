using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGoapCreateParams
    {
        [Required] public string       AgentName  { get; set; }
        public StateVar[]              WorldState { get; set; }
        public GoalDef[]               Goals      { get; set; }
        public ActionDef[]             Actions    { get; set; }
        public string                  AttachTo   { get; set; }
        public string                  SavePath   { get; set; }
    }

    public sealed class StateVar
    {
        public string Key   { get; set; }
        public string Type  { get; set; }
        public string Value { get; set; }
    }

    public sealed class GoalDef
    {
        public string          Name       { get; set; }
        public float           Priority   { get; set; }
        public ConditionPair[] Conditions { get; set; }
    }

    public sealed class ActionDef
    {
        public string          Name          { get; set; }
        public float           Cost          { get; set; } = 1f;
        public ConditionPair[] Preconditions { get; set; }
        public ConditionPair[] Effects       { get; set; }
        public string          MethodName    { get; set; }
    }

    public sealed class ConditionPair
    {
        public string Key   { get; set; }
        public string Value { get; set; }
    }
}
