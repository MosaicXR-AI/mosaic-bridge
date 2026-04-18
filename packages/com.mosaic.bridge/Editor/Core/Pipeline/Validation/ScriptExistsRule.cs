using System.IO;

namespace Mosaic.Bridge.Core.Pipeline.Validation
{
    /// <summary>
    /// Prevents accidental overwriting of existing script files.
    /// Rejects script creation when the target path already exists unless the caller
    /// explicitly sets overwrite=true in parameters.
    /// </summary>
    public sealed class ScriptExistsRule : IValidationRule
    {
        public string Category => "script";

        public ValidationResult Validate(ExecutionContext context)
        {
            // Only applies to "create" operations
            if (context.ToolName == null || !context.ToolName.Contains("create"))
                return ValidationResult.Pass();

            var parameters = context.Parameters;
            if (parameters == null)
                return ValidationResult.Pass();

            var path = (string)parameters["path"]
                    ?? (string)parameters["filePath"];

            if (string.IsNullOrEmpty(path))
                return ValidationResult.Pass();

            // If overwrite is explicitly true, allow it
            var overwriteToken = parameters["overwrite"];
            var overwrite = overwriteToken != null && (bool)overwriteToken;
            if (overwrite)
                return ValidationResult.Pass();

            // Resolve relative paths against the Unity project root
            var fullPath = path;
            if (!Path.IsPathRooted(path))
            {
                // Application.dataPath ends with "/Assets" — go up one level for project root
                var projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                fullPath = Path.Combine(projectRoot, path);
            }

            if (File.Exists(fullPath))
            {
                return ValidationResult.Reject(
                    $"Script already exists at '{path}'. Set overwrite=true or choose a different path.");
            }

            return ValidationResult.Pass();
        }
    }
}
