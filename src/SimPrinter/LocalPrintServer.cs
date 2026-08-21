using System.Net;
using System.Net.Http;
using System.Text;

namespace SimPrinter
{
    /// <summary>
    /// Minimal localhost-only HTTP server that lets the SimPrinter browser extension print
    /// arbitrary text (e.g. a SimBrief performance calculation) with one click. Opt-in via
    /// Settings, off by default. Bound to 127.0.0.1 only - never reachable from the network,
    /// only from processes on this machine (including a browser extension's background script).
    /// </summary>
    public sealed class LocalPrintServer : IDisposable
    {
        public const int Port = 39901;

        // SimCallouts's LocalImportServer, if that app happens to be running - the extension
        // only ever talks to this port, so anything SimCallouts wants (V1/VR out of a takeoff
        // performance calculation) has to be relayed on rather than received directly.
        private const string SimCalloutsImportUrl = "http://127.0.0.1:39902/import-text";

        // Short timeout so a hung/unreachable SimCallouts can't back up print requests -
        // this is a best-effort relay, not something printing should ever wait on.
        private static readonly HttpClient RelayClient = new() { Timeout = TimeSpan.FromMilliseconds(750) };

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;

        /// <summary>Raised with the request body whenever POST /print-text is received.</summary>
        public Action<string>? OnPrintTextRequested { get; set; }

        public void Start()
        {
            if (_listener != null) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
            _cts = null;
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener!.GetContextAsync();
                }
                catch
                {
                    break; // listener was stopped/disposed
                }

                _ = Task.Run(() => HandleRequest(ctx));
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                // The extension's background script calls this from a moz-extension:// origin,
                // not a page context, so CORS wouldn't normally apply - these headers just make
                // the intent explicit and keep things working if that ever changes.
                ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                ctx.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
                ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";

                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    return;
                }

                if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url?.AbsolutePath == "/print-text")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    string body = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(body))
                    {
                        ctx.Response.StatusCode = 400;
                        return;
                    }

                    OnPrintTextRequested?.Invoke(body);
                    _ = RelayToSimCalloutsAsync(body);
                    ctx.Response.StatusCode = 200;
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch
            {
                try { ctx.Response.StatusCode = 500; } catch { /* response already sent/closed */ }
            }
            finally
            {
                ctx.Response.Close();
            }
        }

        /// <summary>
        /// Fire-and-forget relay to SimCallouts. Failing silently is the point here - most
        /// users won't have SimCallouts installed at all, and printing must never be held up
        /// or fail because of this.
        /// </summary>
        private static async Task RelayToSimCalloutsAsync(string body)
        {
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "text/plain");
                await RelayClient.PostAsync(SimCalloutsImportUrl, content);
            }
            catch
            {
                // SimCallouts isn't running, or isn't listening - nothing to do.
            }
        }

        public void Dispose() => Stop();
    }
}
