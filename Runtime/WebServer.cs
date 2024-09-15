using System;
using System.IO;
using WebSocketSharp.Server;

namespace UnityRenderStreamingWebService
{
    public class WebServer
    {
        public HttpServer Server { get; private set; }
        public bool UseWebSocket { get; private set; }
        public string Mode { get; private set; }
        public string Logging { get; private set; }
        WSSignaling ws_signaling;
        HTTPSignaling http_signaling;
        public WebServer(string url, string doc, bool useWebSocket = true, string mode = "public", string logging = "dev")
        {
            UseWebSocket = useWebSocket;
            Mode = mode;

            Server = new HttpServer(url);

            ws_signaling = new WSSignaling();
            ws_signaling.IsPrivate = mode == "private";
            Server.AddWebSocketService<WebSocketHandler>("/", x => { x.Signaling = ws_signaling; });

            http_signaling = new HTTPSignaling();
            http_signaling.IsPrivate = mode == "private";

            Server.DocumentRootPath = doc;
            Server.OnGet += OnGet;
            Server.OnPut += OnPut;
            Server.OnDelete += OnDelete;
            Server.OnPost += OnPost;
        }

        public void Start()
        {
            Server?.Start();
        }
        public void Stop()
        {
            Server?.Stop();
        }

        void OnGet(object sender, HttpRequestEventArgs context)
        {
            string path = context.Request.Url.LocalPath;

            if (path == "/")
                path = "/index.html";

            if (context.TryReadFile(path, out byte[] content))
            {
                context.Response.ContentType = FileContentType.GetMimeType(Path.GetExtension(path));
                context.Response.ContentLength64 = content.Length;
                context.Response.OutputStream.Write(content);
                return;
            }

            if (path == "/config")
            {
                context.Response.WriteJson(new { useWebSocket = UseWebSocket, startupMode = Mode, logging = Logging });
            }
            if (path.StartsWith("/signaling"))
            {
                bool check = http_signaling.CheckSessionId(context.Request, context.Response);
                if (check)
                {
                    if (path == "/signaling/connection")
                    {
                        http_signaling.GetConnection(context.Request, context.Response);
                    }
                    else if (path == "/signaling/offer")
                    {
                        http_signaling.GetOffer(context.Request, context.Response);
                    }
                    else if (path == "/signaling/answer")
                    {
                        http_signaling.GetAnswer(context.Request, context.Response);
                    }
                    else if (path == "/signaling/candidate")
                    {
                        http_signaling.GetCandidate(context.Request, context.Response);
                    }
                    else if (path == "/signaling")
                    {
                        http_signaling.GetAll(context.Request, context.Response);
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
        }

        void OnPut(object sender, HttpRequestEventArgs context)
        {
            string path = context.Request.Url.LocalPath;

            if (path.StartsWith("/signaling"))
            {
                bool check = http_signaling.CheckSessionId(context.Request, context.Response);
                if (check)
                {
                    if (path == "/signaling")
                    {
                        http_signaling.CreateSession(context.Request, context.Response);
                    }
                    else if (path == "/signaling/connection")
                    {
                        http_signaling.CreateConnection(context.Request, context.Response);
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
        }

        void OnDelete(object sender, HttpRequestEventArgs context)
        {
            string path = context.Request.Url.LocalPath;

            if (path.StartsWith("/signaling"))
            {
                bool check = http_signaling.CheckSessionId(context.Request, context.Response);
                if (check)
                {
                    if (path == "/signaling")
                    {
                        http_signaling.DeleteSession(context.Request, context.Response);
                    }
                    else if (path == "/signaling/connection")
                    {
                        http_signaling.DeleteConnection(context.Request, context.Response);
                    }
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
        }
        void OnPost(object sender, HttpRequestEventArgs context)
        {
            string path = context.Request.Url.LocalPath;

            try
            {
                if (path.StartsWith("/signaling"))
                {
                    bool check = http_signaling.CheckSessionId(context.Request, context.Response);
                    if (check)
                    {
                        if (path == "/signaling/offer")
                        {
                            http_signaling.PostOffer(context.Request, context.Response);
                        }
                        else if (path == "/signaling/answer")
                        {
                            http_signaling.PostAnswer(context.Request, context.Response);
                        }
                        else if (path == "/signaling/candidate")
                        {
                            http_signaling.PostCandidate(context.Request, context.Response);
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }

        }
    }
}