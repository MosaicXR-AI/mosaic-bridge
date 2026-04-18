namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGoapCreateResult
    {
        public string ScriptPath      { get; set; }
        public string GameObjectName  { get; set; }
        public int    InstanceId      { get; set; }
        public int    GoalCount       { get; set; }
        public int    ActionCount     { get; set; }
    }
}
