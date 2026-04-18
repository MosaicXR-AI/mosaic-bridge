namespace Mosaic.Bridge.Tools.Reflection
{
    public sealed class ReflectionCallMethodResult
    {
        /// <summary>The full signature of the method that was called.</summary>
        public string MethodSignature { get; set; }

        /// <summary>The serialized return value (safe-serialized for Unity types).</summary>
        public object ReturnValue { get; set; }

        /// <summary>The CLR type name of the return value.</summary>
        public string ReturnType { get; set; }

        /// <summary>Execution time in milliseconds.</summary>
        public double ExecutionTimeMs { get; set; }
    }
}
