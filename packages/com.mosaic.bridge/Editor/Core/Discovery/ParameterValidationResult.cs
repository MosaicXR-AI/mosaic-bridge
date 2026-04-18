namespace Mosaic.Bridge.Core.Discovery
{
    public sealed class ParameterValidationResult
    {
        public bool IsValid { get; }
        public object Value { get; }        // the bound, typed parameter object; null on failure
        public string ErrorCode { get; }    // from ErrorCodes; null on success
        public string ErrorMessage { get; }

        private ParameterValidationResult(bool isValid, object value, string errorCode, string errorMessage)
        {
            IsValid = isValid;
            Value = value;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public static ParameterValidationResult Ok(object value) =>
            new ParameterValidationResult(true, value, null, null);

        public static ParameterValidationResult Fail(string code, string message) =>
            new ParameterValidationResult(false, null, code, message);
    }
}
