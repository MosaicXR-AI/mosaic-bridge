using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public sealed class ConsoleGetErrorsResult
    {
        public List<ConsoleEntry> Entries { get; set; }
        public int Count { get; set; }
        public bool ReflectionAvailable { get; set; }
    }

    public sealed class ConsoleEntry
    {
        public string Message { get; set; }
        public string Level { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
    }
}
