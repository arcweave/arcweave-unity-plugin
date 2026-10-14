using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Arcweave.Tests
{
    ///<summary>A local stub of the asset file endpoint of Arcweave's web API: GET /api/{hash}/asset/{id}/file.</summary>
    public sealed class StubApiServer : IDisposable
    {
        public sealed class Response
        {
            public int status = 200;
            public byte[] body = new byte[0];
            public string retryAfter;
        }

        private readonly HttpListener listener = new HttpListener();
        private readonly object sync = new object();
        private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, Queue<Response>> pendingResponses = new Dictionary<string, Queue<Response>>();
        private readonly List<KeyValuePair<string, string>> requests = new List<KeyValuePair<string, string>>();
        private TaskCompletionSource<bool> hold;

        public string ApiUrl { get; }

        public StubApiServer() {
            var port = FreePort();
            ApiUrl = $"http://127.0.0.1:{port}/api/";
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            Task.Run(Listen);
        }

        ///<summary>Serves the file of an asset id with status 200.</summary>
        public void SetFile(string assetId, byte[] body) {
            lock ( sync ) { files[assetId] = body; }
        }

        ///<summary>Serves a response once for an asset id, before its file.</summary>
        public void Enqueue(string assetId, Response response) {
            lock ( sync ) {
                if ( !pendingResponses.TryGetValue(assetId, out var queue) ) {
                    pendingResponses[assetId] = queue = new Queue<Response>();
                }
                queue.Enqueue(response);
            }
        }

        ///<summary>Holds every response until Release is called.</summary>
        public void Hold() {
            lock ( sync ) { hold = new TaskCompletionSource<bool>(); }
        }

        public void Release() {
            TaskCompletionSource<bool> released;
            lock ( sync ) {
                released = hold;
                hold = null;
            }
            released?.TrySetResult(true);
        }

        public int RequestCount => Requests.Count;
        public int RequestCountFor(string assetId) => Requests.Count(request => request.Key == assetId);
        public IEnumerable<string> Authorizations => Requests.Select(request => request.Value);

        private List<KeyValuePair<string, string>> Requests {
            get { lock ( sync ) { return requests.ToList(); } }
        }

        public void Dispose() {
            Release();
            try { listener.Stop(); } catch ( ObjectDisposedException ) { }
            listener.Close();
        }

        private async Task Listen() {
            while ( listener.IsListening ) {
                HttpListenerContext context;
                try {
                    context = await listener.GetContextAsync();
                }
                catch ( Exception ) {
                    return; // stopped
                }
                _ = Task.Run(() => Handle(context));
            }
        }

        private void Handle(HttpListenerContext context) {
            // /api/{hash}/asset/{id}/file
            var segments = context.Request.Url.AbsolutePath.Trim('/').Split('/');
            var assetId = segments.Length == 5 && segments[0] == "api" && segments[2] == "asset" && segments[4] == "file"
                ? Uri.UnescapeDataString(segments[3])
                : null;

            Response response;
            Task waitFor;
            lock ( sync ) {
                requests.Add(new KeyValuePair<string, string>(assetId, context.Request.Headers["Authorization"]));
                waitFor = hold?.Task;
                if ( assetId != null && pendingResponses.TryGetValue(assetId, out var queue) && queue.Count > 0 ) {
                    response = queue.Dequeue();
                } else if ( assetId != null && files.TryGetValue(assetId, out var file) ) {
                    response = new Response { body = file };
                } else {
                    response = new Response { status = 404 };
                }
            }
            waitFor?.Wait();

            try {
                context.Response.StatusCode = response.status;
                if ( response.retryAfter != null ) { context.Response.AddHeader("Retry-After", response.retryAfter); }
                context.Response.ContentLength64 = response.body.Length;
                context.Response.OutputStream.Write(response.body, 0, response.body.Length);
                context.Response.Close();
            }
            catch ( Exception ) {
                // The client went away, eg. the request was aborted
            }
        }

        private static int FreePort() {
            var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            var port = ( (IPEndPoint)socket.LocalEndpoint ).Port;
            socket.Stop();
            return port;
        }
    }
}
