namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public sealed class ConsoleLogPathParams
    {
        /// <summary>Number of recent lines to return. 0 = path only.</summary>
        public int TailLines { get; set; } = 0;
    }
}
