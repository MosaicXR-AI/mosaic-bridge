namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiUtilityCreateResult
    {
        public string ScriptPath      { get; set; }
        public string GameObjectName  { get; set; }
        public int    InstanceId      { get; set; }
        public int    ActionCount     { get; set; }
        public int    InputCount      { get; set; }
    }
}
