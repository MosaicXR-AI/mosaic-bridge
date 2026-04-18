using System;
using System.Security.Cryptography;
using UnityEngine;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// MonoBehaviour that bootstraps the Mosaic Bridge runtime dispatch pipeline.
    /// Auto-creates via RuntimeInitializeOnLoadMethod after scene load.
    /// Marked DontDestroyOnLoad so it persists across scene changes.
    /// </summary>
    public sealed class MosaicBridgeRuntime : MonoBehaviour
    {
        private static MosaicBridgeRuntime _instance;

        private RuntimeLogger _logger;
        private RuntimeNonceCache _nonceCache;
        private RuntimeHmacAuthenticator _authenticator;
        private RuntimeDispatcher _dispatcher;
        private RuntimeHttpServer _server;
        private RuntimeKnowledgeProvider _knowledgeProvider;
        private MosaicRuntimeConfig _config;

        /// <summary>Singleton instance. Null until runtime initialization.</summary>
        public static MosaicBridgeRuntime Instance => _instance;

        /// <summary>True when the runtime bridge is listening for requests.</summary>
        public bool IsRunning => _server != null && _server.IsRunning;

        /// <summary>The port the runtime server is bound to.</summary>
        public int Port => _server?.Port ?? 0;

        /// <summary>The runtime dispatcher for testing or extension.</summary>
        public RuntimeDispatcher Dispatcher => _dispatcher;

        /// <summary>The knowledge provider for runtime KB queries.</summary>
        public RuntimeKnowledgeProvider KnowledgeProvider => _knowledgeProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            // Skip in editor play mode - the editor bridge handles dispatch there
#if UNITY_EDITOR
            return;
#else
            if (_instance != null)
                return;

            var go = new GameObject("[MosaicBridgeRuntime]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MosaicBridgeRuntime>();
#endif
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Initialize()
        {
            _logger = new RuntimeLogger();

            // Load config from Resources
            _config = Resources.Load<MosaicRuntimeConfig>("MosaicBridge/MosaicRuntimeConfig");
            if (_config == null)
            {
                _logger.Warn("MosaicRuntimeConfig not found in Resources/MosaicBridge/. Using defaults.");
                _config = ScriptableObject.CreateInstance<MosaicRuntimeConfig>();
            }

            // Resolve or generate secret
            var secretBase64 = _config.Secret;
            byte[] secretBytes;
            if (string.IsNullOrEmpty(secretBase64))
            {
                secretBytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(secretBytes);
                }
                secretBase64 = Convert.ToBase64String(secretBytes);
                _config.Secret = secretBase64;
                _logger.Info("Auto-generated runtime HMAC secret");
            }
            else
            {
                try
                {
                    secretBytes = Convert.FromBase64String(secretBase64);
                }
                catch (FormatException)
                {
                    _logger.Error("Invalid Base64 secret in MosaicRuntimeConfig. Generating new secret.");
                    secretBytes = new byte[32];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(secretBytes);
                    }
                    secretBase64 = Convert.ToBase64String(secretBytes);
                    _config.Secret = secretBase64;
                }
            }

            // Initialize components
            _nonceCache = new RuntimeNonceCache();
            _authenticator = new RuntimeHmacAuthenticator(secretBytes, _nonceCache, _logger);
            _dispatcher = new RuntimeDispatcher(_logger);
            _knowledgeProvider = new RuntimeKnowledgeProvider(_logger);

            // Start HTTP server
            _server = new RuntimeHttpServer(_authenticator, _dispatcher, _logger);
            try
            {
                _server.Start(_config.Port);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to start runtime HTTP server: {ex.Message}", ex);
                return;
            }

            // Write discovery file
            try
            {
                RuntimeDiscoveryWriter.Write(_server.Port, secretBase64, _logger);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to write discovery file: {ex.Message}");
            }

            _logger.Info($"Mosaic Bridge Runtime initialized on port {_server.Port}");
        }

        private void Update()
        {
            _dispatcher?.ProcessPendingRequests();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                Shutdown();
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void Shutdown()
        {
            try
            {
                _dispatcher?.Drain();
                _server?.Stop();
                RuntimeDiscoveryWriter.Delete(_logger);
                _logger?.Info("Mosaic Bridge Runtime shut down");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error during runtime shutdown: {ex.Message}", ex);
            }
        }
    }
}
