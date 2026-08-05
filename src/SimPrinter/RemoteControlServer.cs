using System.Net;
using System.Text;

namespace SimPrinter
{
    /// <summary>
    /// Minimal localhost-only HTTP server so external tools (e.g. an Elgato Stream Deck
    /// plugin) can trigger prints while SimPrinter is running. HttpListener is bound to
    /// "localhost" specifically, not a wildcard host, so it is never reachable from
    /// outside this machine.
    /// </summary>
    public sealed class RemoteControlServer : IDisposable
    {
        public const int Port = 47653;

        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();

        public Func<bool> HasFlightPlan { get; set; } = () => false;
        public Action? OnPrintFlightPlan { get; set; }
        public Action? OnPrintPreliminary { get; set; }
        public Action? OnPrintFinal { get; set; }

        public void Start()
        {
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            try
            {
                _listener.Start();
            }
            catch
            {
                // Most likely another SimPrinter instance already owns the port - remote
                // control just won't be available for this instance, which is fine.
                return;
            }

            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    break; // listener stopped/disposed
                }

                _ = Task.Run(() => HandleRequest(context));
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url?.AbsolutePath ?? "";

                if (context.Request.HttpMethod == "GET" && path == "/ping")
                {
                    Respond(context, 200, "SimPrinter");
                    return;
                }

                if (context.Request.HttpMethod != "POST")
                {
                    Respond(context, 405, "Method not allowed");
                    return;
                }

                Action? action = path switch
                {
                    "/print/flightplan" => OnPrintFlightPlan,
                    "/print/preliminary" => OnPrintPreliminary,
                    "/print/final" => OnPrintFinal,
                    _ => null
                };

                if (action == null)
                {
                    Respond(context, 404, "Unknown endpoint");
                    return;
                }

                if (!HasFlightPlan())
                {
                    Respond(context, 409, "No flight plan loaded");
                    return;
                }

                action();
                Respond(context, 200, "OK");
            }
            catch
            {
                try { Respond(context, 500, "Internal error"); } catch { /* response already closed */ }
            }
        }

        private static void Respond(HttpListenerContext context, int statusCode, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* already stopped */ }
            try { _listener.Close(); } catch { /* already closed */ }
            _cts.Dispose();
        }
    }
}
