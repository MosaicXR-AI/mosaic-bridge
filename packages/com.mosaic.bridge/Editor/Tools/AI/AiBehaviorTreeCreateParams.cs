using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiBehaviorTreeCreateParams
    {
        [Required] public string          Name       { get; set; }
        [Required] public TreeNodeDef     RootNode   { get; set; }
        public string                     AttachTo   { get; set; }
        public BlackboardVar[]            Blackboard { get; set; }
        public string                     SavePath   { get; set; }
    }

    public sealed class TreeNodeDef
    {
        public string        Type        { get; set; }
        public string        Name        { get; set; }
        public TreeNodeDef[] Children    { get; set; }
        public string        Action      { get; set; }
        public string        Condition   { get; set; }
        public int?          RepeatCount { get; set; }
    }

    public sealed class BlackboardVar
    {
        public string Key          { get; set; }
        public string Type         { get; set; }
        public string DefaultValue { get; set; }
    }
}
