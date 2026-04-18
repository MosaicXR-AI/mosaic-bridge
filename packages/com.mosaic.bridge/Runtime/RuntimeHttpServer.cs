using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// HTTP listener for compiled runtime builds. Binds to loopback 127.0.0.1
    /// on a configurable port, authenticates via HMAC, and dispatches requests
    /// to the <see cref="RuntimeDispatcher"/> for main-thread processing.
    /// </summary>
    public sealed class RuntimeHttpServer
    {
        private readonly RuntimeHmacAuthenticator _authenticator;
        private readonly RuntimeDispatcher _dispatcher;
        private readonly RuntimeLogger _logger;

        private HttpListener _listener;
        private Thread _loopThread;

        /// <summary>The port the server is bound to. 0 until Start() succeeds.</summary>
        public int Port { get; private set; }

        /// <summary>True while the listen loop is running.</summary>
        public bool IsRunning { get; private set; }

        public RuntimeHttpServer(
            RuntimeHmacAuthenticator authenticator,
            RuntimeDispatcher dispatcher,
            RuntimeLogger logger)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Binds to <paramref name="preferredPort"/> if non-zero and available,
        /// otherwise falls back to an ephemeral loopback port. Starts the listen loop.
        /// </summary>
        public void Start(int preferredPort = 0)
        {
            if (IsRunning)
                throw new InvalidOperationException("RuntimeHttpServer is already running.");

            int port = TryBindPort(preferredPort);
            Port = port;
            IsRunning = true;

            _loopThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Mosaic.Bridge.Runtime.ListenLoop"
            };
            _loopThread.Start();

            _logger.Info($"RuntimeHttpServer started on port {port}");
        }

        /// <summary>
        /// Stops the listen loop and releases the port.
        /// </summary>
        public void Stop()
        {
            IsRunning = false;

            try { _listener?.Stop(); }
            catch { /* suppress */ }

            _loopThread?.Join(2000);

            _logger.Info("RuntimeHttpServer stopped");
        }

        private int TryBindPort(int preferred)
        {
            if (preferred > 0)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{preferred}/");
                    _listener.Start();
                    return preferred;
                }
                catch (HttpListenerException)
                {
                    _logger.Warn($"Preferred port {preferred} is in use - falling back to ephemeral port");
                    try { _listener?.Stop(); } catch { /* suppress */ }
                    _listener = null;
                }
            }

            // Ephemeral fallback
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            return port;
        }

        private void ListenLoop()
        {
            while (IsRunning)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext(); // blocking
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }

                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
        }

        private static bool IsHealthRoute(HttpListenerContext context)
        {
            var path = context.Request.RawUrl?.Split('?')[0].TrimEnd('/') ?? string.Empty;
            return context.Request.HttpMethod == "GET" && path == "/health";
        }

        private void HandleRequest(HttpListenerContext context)
        {
            // Health endpoint is unauthenticated
            RuntimeHmacAuthenticator.AuthResult authResult;
            if (IsHealthRoute(context))
            {
                authResult = RuntimeHmacAuthenticator.AuthResult.Ok(Array.Empty<byte>());
            }
            else
            {
                authResult = _authenticator.Authenticate(context.Request);
            }

            if (!authResult.IsAuthenticated)
            {
                try
                {
                    var reason = authResult.FailureReason ?? "unauthorized";
                    var body401 = Encoding.UTF8.GetBytes($"{{\"error\":\"{reason}\"}}");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    context.Response.OutputStream.Write(body401, 0, body401.Length);
                    context.Response.Close();
                }
                catch { /* suppress */ }
                return;
            }

            var clientId = context.Request.Headers["X-Mosaic-Client"] ?? "default";

            var handlerRequest = new RuntimeHandlerRequest
            {
                Method = context.Request.HttpMethod.ToUpperInvariant(),
                RawUrl = context.Request.RawUrl,
                Body = authResult.Body ?? Array.Empty<byte>(),
                ClientId = clientId
            };

            var task = _dispatcher.EnqueueAsync(handlerRequest, CancellationToken.None);
            RuntimeHandlerResponse response;

            if (task == null)
            {
                // Queue full - backpressure
                response = new RuntimeHandlerResponse
                {
                    StatusCode = 503,
                    ContentType = "application/json",
                    Body = "{\"error\":\"BRIDGE_BACKPRESSURE\",\"retryAfter\":5}",
                    Headers = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "Retry-After", "5" }
                    }
                };
            }
            else
            {
                response = task.GetAwaiter().GetResult(); // blocking on ThreadPool - acceptable
            }

            try
            {
                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = response.ContentType;
                if (response.Headers != null)
                {
                    foreach (var kvp in response.Headers)
                        context.Response.Headers[kvp.Key] = kvp.Value;
                }
                var bodyBytes = Encoding.UTF8.GetBytes(response.Body ?? string.Empty);
                context.Response.OutputStream.Write(bodyBytes, 0, bodyBytes.Length);
                context.Response.Close();
            }
            catch { /* suppress */ }

            _logger.Trace($"[{handlerRequest.Method}] {handlerRequest.RawUrl} -> {response.StatusCode}");
        }
    }
}
