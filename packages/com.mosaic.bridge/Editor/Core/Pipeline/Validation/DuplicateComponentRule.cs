using System;
using UnityEngine;

namespace Mosaic.Bridge.Core.Pipeline.Validation
{
    /// <summary>
    /// Warns when adding a component that already exists on the target GameObject.
    /// Runs on the main thread (pipeline stages execute via EditorApplication.update dispatch),
    /// so Unity API access is safe.
    /// </summary>
    public sealed class DuplicateComponentRule : IValidationRule
    {
        public string Category => "component";

        public ValidationResult Validate(ExecutionContext context)
        {
            // Only applies to "add" operations
            if (context.ToolName == null || !context.ToolName.Contains("add"))
                return ValidationResult.Pass();

            var parameters = context.Parameters;
            if (parameters == null)
                return ValidationResult.Pass();

            var gameObjectName = (string)parameters["gameObjectName"]
                              ?? (string)parameters["name"];
            var componentType = (string)parameters["componentType"]
                             ?? (string)parameters["type"];

            if (string.IsNullOrEmpty(gameObjectName) || string.IsNullOrEmpty(componentType))
                return ValidationResult.Pass();

            var go = GameObject.Find(gameObjectName);
            if (go == null)
                return ValidationResult.Pass(); // let the tool handler report NOT_FOUND

            var type = ResolveComponentType(componentType);
            if (type == null)
                return ValidationResult.Pass(); // let the tool handler report the bad type

            if (go.GetComponent(type) != null)
            {
                return ValidationResult.Warn(
                    $"GameObject '{gameObjectName}' already has a {componentType} component. " +
                    "Adding a duplicate may cause issues.");
            }

            return ValidationResult.Pass();
        }

        private static Type ResolveComponentType(string typeName)
        {
            // Try UnityEngine first, then a general search
            var type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null)
                return type;

            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (type != null)
                return type;

            // Try the name as-is (fully qualified or assembly-qualified)
            type = Type.GetType(typeName);
            return type;
        }
    }
}
