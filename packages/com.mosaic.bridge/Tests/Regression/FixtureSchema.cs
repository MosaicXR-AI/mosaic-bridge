using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Tests.Regression
{
    /// <summary>
    /// Deserialization model for regression fixture JSON files.
    /// Each fixture defines a single tool invocation with expected response checks.
    /// </summary>
    public sealed class FixtureSchema
    {
        /// <summary>Unique identifier for this fixture (e.g., "gameobject_create_basic").</summary>
        [JsonProperty("fixture")]
        public string Fixture { get; set; }

        /// <summary>Full tool name including mosaic_ prefix (e.g., "mosaic_gameobject_create").</summary>
        [JsonProperty("tool")]
        public string Tool { get; set; }

        /// <summary>Tool category (e.g., "gameobject", "component", "scene").</summary>
        [JsonProperty("category")]
        public string Category { get; set; }

        /// <summary>Human-readable description of what this fixture tests.</summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>Parameters to pass to the tool.</summary>
        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>Expected response assertions.</summary>
        [JsonProperty("expectations")]
        public FixtureExpectations Expectations { get; set; }

        /// <summary>Cleanup action identifier (e.g., "delete_created_object"). Null if no cleanup needed.</summary>
        [JsonProperty("cleanup")]
        public string Cleanup { get; set; }

        /// <summary>Tags for filtering (e.g., "smoke", "core", "destructive").</summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        /// <summary>
        /// Optional setup steps that must run before this fixture.
        /// Each step is a mini tool invocation used to create preconditions.
        /// </summary>
        [JsonProperty("setup")]
        public List<FixtureSetupStep> Setup { get; set; }
    }

    /// <summary>
    /// Defines the expected response from a tool invocation.
    /// </summary>
    public sealed class FixtureExpectations
    {
        /// <summary>Whether the tool should return success=true.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; } = true;

        /// <summary>Expected HTTP status code.</summary>
        [JsonProperty("statusCode")]
        public int StatusCode { get; set; } = 200;

        /// <summary>Fields that must be present in the response data object.</summary>
        [JsonProperty("dataFields")]
        public List<string> DataFields { get; set; }

        /// <summary>Exact value checks on the response data object (key = field name, value = expected value).</summary>
        [JsonProperty("dataChecks")]
        public Dictionary<string, object> DataChecks { get; set; }

        /// <summary>Expected error code when success is false.</summary>
        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; }
    }

    /// <summary>
    /// A setup step that runs a tool to create preconditions before the main fixture.
    /// </summary>
    public sealed class FixtureSetupStep
    {
        /// <summary>Full tool name including mosaic_ prefix.</summary>
        [JsonProperty("tool")]
        public string Tool { get; set; }

        /// <summary>Parameters to pass to the setup tool.</summary>
        [JsonProperty("parameters")]
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Optional key to store the setup result under, so fixtures can reference
        /// values from setup steps (e.g., an InstanceId).
        /// </summary>
        [JsonProperty("storeAs")]
        public string StoreAs { get; set; }
    }
}
