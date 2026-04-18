namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavIKFabrikResult
    {
        public string  SolverScriptPath { get; set; }
        public int     ChainLength      { get; set; }
        public int     RootInstanceId   { get; set; }
        public float[] TargetPosition   { get; set; }
    }
}
