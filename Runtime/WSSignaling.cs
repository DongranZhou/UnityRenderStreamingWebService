using System.Collections.Generic;
namespace UnityRenderStreamingWebService
{
    public class WSSignaling
    {
        public bool IsPrivate { get; set; }
        public Dictionary<WebSocketHandler, List<string>> Clients { get; private set; } = new Dictionary<WebSocketHandler, List<string>>();
        public Dictionary<string, WebSocketHandler[]> ConnectionPair { get; private set; } = new Dictionary<string, WebSocketHandler[]>();
    }
}