using UnityEngine;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// ScriptableObject holding runtime bridge configuration.
    /// Place in a Resources/ folder so it can be loaded at runtime via
    /// <c>Resources.Load&lt;MosaicRuntimeConfig&gt;("MosaicBridge/MosaicRuntimeConfig")</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MosaicRuntimeConfig",
        menuName = "Mosaic Bridge/Runtime Config",
        order = 200)]
    public sealed class MosaicRuntimeConfig : ScriptableObject
    {
        [Header("Network")]
        [Tooltip("HTTP listener port for the runtime bridge. 0 = auto-select ephemeral port.")]
        [SerializeField] private int _port = 8300;

        [Tooltip("HMAC shared secret (Base64). Auto-generated on first run if left empty.")]
        [SerializeField] private string _secret = "";

        [Header("Tool Categories")]
        [Tooltip("Tool categories enabled at runtime. Empty = all runtime-safe categories.")]
        [SerializeField] private string[] _enabledCategories = new string[]
        {
            "gameobject",
            "component",
            "physics",
            "scene",
            "lighting",
            "material",
            "audio",
            "animation",
            "ui",
            "navigation",
            "terrain",
            "particle",
            "camera",
            "search"
        };

        /// <summary>HTTP listener port for the runtime bridge. Default 8300.</summary>
        public int Port => _port;

        /// <summary>HMAC shared secret (Base64). Empty triggers auto-generation.</summary>
        public string Secret
        {
            get => _secret;
            set => _secret = value;
        }

        /// <summary>Tool categories enabled at runtime.</summary>
        public string[] EnabledCategories => _enabledCategories;
    }
}
