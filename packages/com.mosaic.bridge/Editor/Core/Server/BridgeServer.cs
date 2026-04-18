using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Authentication;

namespace Mosaic.Bridge.Core.Server
{
    /// <summary>
    /// HTTP listener that authenticates inbound requests via HMAC and dispatches them to an
    /// <see cref="IRequestHandler"/>. Binds to a random loopback port on Start().
    /// </summary>
    public sealed class BridgeServer
    {
        private readonly HmacAuthenticator _authenticator;
        private readonly IMosaicLogger _logger;
        private volatile IRequestHandler _handler;
        private readonly RateLimiter _rateLimiter;

        private HttpListener _listener;
        private Thread _loopThread;

        /// <summary>The port the server is bound to. 0 until <see cref="Start"/> succeeds.</summary>
        public int Port { get; private set; }

        /// <summary>True while the listen loop is running.</summary>
        public bool IsRunning { get; private set; }

        public BridgeServer(HmacAuthenticator authenticator, IMosaicLogger logger, IRequestHandler handler = null, RateLimiter rateLimiter = null)
        {
            _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handler = handler;
            _rateLimiter = rateLimiter ?? new RateLimiter();
        }

        /// <summary>
        /// Replaces the active handler at any time, thread-safe. The server does not need to
        /// be stopped and restarted — the new handler is visible to the next dispatched request.
        /// </summary>
        public void SetHandler(IRequestHandler handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Binds to <paramref name="preferredPort"/> if non-zero and available, otherwise
        /// falls back to an ephemeral loopback port. Launches the background listen loop.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if already running.</exception>
        public void Start(int preferredPort = 0)
        {
            if (IsRunning)
                throw new InvalidOperationException("BridgeServer is already running.");

            int port = TryBindPort(preferredPort);

            Port = port;
            IsRunning = true;

            _loopThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Mosaic.Bridge.ListenLoop"
            };
            _loopThread.Start();

            _logger.Info($"BridgeServer started on port {port}");
        }

        private int TryBindPort(int preferred)
        {
            // Try the preferred port and the next 9 sequential ports before giving up
            // to ephemeral. This keeps the port predictable (8282..8291 typical range)
            // so MCP config and debugging via curl stays simple, while still tolerating
            // the occasional conflict with AssetImportWorker inheriting FDs.
            if (preferred > 0)
            {
                const int sequentialRange = 10;
                for (int offset = 0; offset < sequentialRange; offset++)
                {
                    int candidate = preferred + offset;
                    try
                    {
                        _listener = new HttpListener();
                        _listener.Prefixes.Add($"http://127.0.0.1:{candidate}/");
                        _listener.Start();
                        if (offset > 0)
                            _logger.Warn($"Preferred port {preferred} in use — bound to {candidate} instead");
                        return candidate;
                    }
                    catch (Exception ex) when (ex is HttpListenerException || ex is System.Net.Sockets.SocketException)
                    {
                        try { _listener?.Stop(); } catch { }
                        _listener = null;
                    }
                }
                _logger.Warn($"Ports {preferred}..{preferred + sequentialRange - 1} all in use — falling back to ephemeral");
            }

            // Ephemeral fallback: probe TcpListener to find a free port
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            return port;
        }

        /// <summary>
        /// Signals the listen loop to stop, halts the <see cref="HttpListener"/>, and waits
        /// up to 2 seconds for the loop thread to exit.
        /// </summary>
        public void Stop()
        {
            IsRunning = false;

            try
            {
                _listener?.Stop();
            }
            catch { }

            _loopThread?.Join(2000);

            _logger.Info("BridgeServer stopped");
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

        private static bool IsPublicRoute(HttpListenerContext context)
        {
            var path = context.Request.RawUrl?.Split('?')[0].TrimEnd('/') ?? string.Empty;
            return context.Request.HttpMethod == "GET" && path == "/health";
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HmacAuthenticator.AuthResult authResult;
            if (IsPublicRoute(context))
            {
                // Health endpoint is unauthenticated — public liveness probe
                authResult = HmacAuthenticator.AuthResult.Ok(Array.Empty<byte>());
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
                catch { }
                return;
            }

            // Story 8.2: Per-client rate limiting (100 req/s default)
            var clientId = context.Request.Headers["X-Mosaic-Client"] ?? "default";
            if (!_rateLimiter.TryConsume(clientId))
            {
                try
                {
                    var body429 = Encoding.UTF8.GetBytes(
                        "{\"error\":\"RATE_LIMITED\",\"message\":\"Too many requests. Try again shortly.\"}");
                    context.Response.StatusCode = 429;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Retry-After"] = "1";
                    context.Response.OutputStream.Write(body429, 0, body429.Length);
                    context.Response.Close();
                }
                catch { }
                return;
            }

            var handlerRequest = new HandlerRequest
            {
                Method = context.Request.HttpMethod.ToUpperInvariant(),
                RawUrl = context.Request.RawUrl,
                Body = authResult.Body ?? Array.Empty<byte>(),
                // Story 1.11 + 8.2: Client identity for round-robin dispatch and rate limiting.
                ClientId = clientId
            };

            var handlerToUse = _handler; // read volatile once
            HandlerResponse response;
            if (handlerToUse == null)
            {
                response = HandlerResponse.NotReady();
            }
            else
            {
                response = handlerToUse.HandleAsync(handlerRequest, CancellationToken.None)
                                       .GetAwaiter().GetResult(); // blocking on ThreadPool — acceptable
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
            catch { }

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.Trace($"[{handlerRequest.Method}] {handlerRequest.RawUrl} → {response.StatusCode}");
            }
        }
    }
}
