using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Loads knowledge base JSON from Resources/ at runtime.
    /// Uses <c>Resources.Load&lt;TextAsset&gt;</c> instead of the editor's AssetDatabase.
    /// Lazy-loaded and cached.
    /// </summary>
    public sealed class RuntimeKnowledgeProvider
    {
        private Dictionary<string, string> _cache = new Dictionary<string, string>();
        private readonly RuntimeLogger _logger;

        public RuntimeKnowledgeProvider(RuntimeLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Loads a knowledge base JSON file from Resources.
        /// Path is relative to a Resources/ folder, e.g. "MosaicBridge/Knowledge/physics/constants".
        /// Returns the raw JSON string, or null if not found.
        /// </summary>
        public string LoadJson(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return null;

            if (_cache.TryGetValue(resourcePath, out var cached))
                return cached;

            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                _logger.Warn($"KnowledgeBase resource not found: {resourcePath}");
                return null;
            }

            var json = asset.text;
            _cache[resourcePath] = json;
            _logger.Trace($"KnowledgeBase loaded: {resourcePath} ({json.Length} chars)");
            return json;
        }

        /// <summary>
        /// Loads the physics constants knowledge base.
        /// </summary>
        public string GetPhysicsConstants()
        {
            return LoadJson("MosaicBridge/Knowledge/physics/constants");
        }

        /// <summary>
        /// Loads the PBR materials knowledge base.
        /// </summary>
        public string GetPbrMaterials()
        {
            return LoadJson("MosaicBridge/Knowledge/rendering/pbr-materials");
        }

        /// <summary>
        /// Returns contextual summary for a KB category.
        /// </summary>
        public string GetContextForCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return null;

            switch (category.ToLowerInvariant())
            {
                case "physics":
                    return GetPhysicsConstants();
                case "rendering":
                    return GetPbrMaterials();
                default:
                    _logger.Trace($"Unknown KB category: {category}");
                    return null;
            }
        }

        /// <summary>
        /// Clears the in-memory cache so data is reloaded on next access.
        /// </summary>
        public void InvalidateCache()
        {
            _cache.Clear();
        }
    }
}
