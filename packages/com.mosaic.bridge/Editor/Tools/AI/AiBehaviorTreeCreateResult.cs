namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiBehaviorTreeCreateResult
    {
        public string ScriptPath         { get; set; }
        public string GameObjectName     { get; set; }
        public int    InstanceId         { get; set; }
        public int    NodeCount          { get; set; }
        public int    BlackboardVarCount { get; set; }
    }
}
