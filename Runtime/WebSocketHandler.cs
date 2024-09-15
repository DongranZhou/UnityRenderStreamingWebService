using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace UnityRenderStreamingWebService
{
    public class WebSocketHandler : WebSocketBehavior
    {
        public WSSignaling Signaling { get; set; }


        List<string> GetOrCreateConnectionIds()
        {
            if (!Signaling.Clients.ContainsKey(this))
                Signaling.Clients[this] = new List<string>();
            return Signaling.Clients[this];
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            string clientId = ID;

            WSMessage message = JsonConvert.DeserializeObject<WSMessage>(e.Data);
            try
            {
                switch (message.type)
                {
                    case "connect":
                        OnConnect(message.connectionId);
                        break;
                    case "disconnect":
                        OnDisconnect(message.connectionId);
                        break;
                    case "offer":
                        OnOffer(message.data);
                        break;
                    case "answer":
                        OnAnswer(message.data);
                        break;
                    case "candidate":
                        OnCandidate(message.data);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void SendMessage(object obj)
        {
            string text = JsonConvert.SerializeObject(obj);
            Send(text);
        }

        protected override void OnOpen()
        {
            Signaling.Clients[this] = new List<string>();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            var connectionIds = GetOrCreateConnectionIds();
            foreach (var connectionId in connectionIds)
            {
                var pair = Signaling.ConnectionPair[connectionId];
                if (pair != null)
                {
                    var otherSessionWs = pair[0].ID == ID ? pair[1] : pair[0];
                    otherSessionWs?.Send(JsonConvert.SerializeObject(new { type = "disconnect", connectionId = connectionId }));
                }
                Signaling.ConnectionPair.Remove(connectionId);
            }
            Signaling.Clients.Remove(this);
        }

        void OnConnect(string connectionId)
        {
            bool polite = true;
            if (Signaling.IsPrivate)
            {
                if (Signaling.ConnectionPair.ContainsKey(connectionId))
                {
                    var pair = Signaling.ConnectionPair[connectionId];

                    if (pair[0] != null && pair[1] != null)
                    {
                        SendMessage(new { type = "error", message = $"{connectionId}: This connection id is already used." });
                        return;
                    }
                    else if (pair[0] != null)
                    {
                        Signaling.ConnectionPair[connectionId] = new WebSocketHandler[2] { pair[0], this };
                    }
                }
                else
                {
                    Signaling.ConnectionPair[connectionId] = new WebSocketHandler[2] { this, null };
                    polite = false;
                }
            }
            List<string> connectionIds = GetOrCreateConnectionIds();
            if (!connectionIds.Contains(connectionId))
                connectionIds.Add(connectionId);
            SendMessage(new { type = "connect", connectionId = connectionId, polite = polite });
        }

        void OnDisconnect(string connectionId)
        {
            var connectionIds = GetOrCreateConnectionIds();
            connectionIds.Remove(connectionId);
            if (Signaling.ConnectionPair.ContainsKey(connectionId))
            {
                var pair = Signaling.ConnectionPair[connectionId];
                var otherSessionWs = pair[0].ID == ID ? pair[1] : pair[0];
                otherSessionWs?.SendMessage(new { type = "disconnect", connectionId = connectionId });
            }
            Signaling.ConnectionPair.Remove(connectionId);
            SendMessage(new { type = "disconnect", connectionId = connectionId });
        }

        void OnOffer(WSMessageData message)
        {
            var connectionId = message.connectionId;
            var newOffer = new Offer(message.sdp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), false);

            if (Signaling.IsPrivate)
            {
                if (Signaling.ConnectionPair.ContainsKey(connectionId))
                {
                    var pair = Signaling.ConnectionPair[connectionId];
                    var otherSessionWs = pair[0].ID == ID ? pair[1] : pair[0];
                    if (otherSessionWs != null)
                    {
                        newOffer.polite = true;
                        otherSessionWs?.SendMessage(new { from = connectionId, to = "", type = "offer", data = newOffer });
                    }
                }
                return;
            }

            Signaling.ConnectionPair[connectionId] = new WebSocketHandler[2] { this, null };
            foreach (var kv in Signaling.Clients)
            {
                if (kv.Key != this)
                {
                    kv.Key.SendMessage(new { from = connectionId, to = "", type = "offer", data = newOffer });
                }
            }
        }

        void OnAnswer(WSMessageData message)
        {
            var connectionId = message.connectionId;
            var connectionIds = GetOrCreateConnectionIds();
            if (!connectionIds.Contains(connectionId))
                connectionIds.Add(connectionId);
            var newAnswer = new Answer(message.sdp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            if (!Signaling.ConnectionPair.ContainsKey(connectionId))
                return;

            var pair = Signaling.ConnectionPair[connectionId];
            var otherSessionWs = pair[0].ID == ID ? pair[1] : pair[0];

            if (!Signaling.IsPrivate)
            {
                Signaling.ConnectionPair[connectionId] = new WebSocketHandler[2] { otherSessionWs, this };
            }

            otherSessionWs.SendMessage(new { from = connectionId, to = "", type = "answer", data = newAnswer });
        }

        void OnCandidate(WSMessageData message)
        {
            var connectionId = message.connectionId;
            var candidate = new Candidate(message.candidate, message.sdpMLineIndex, message.sdpMid, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            if (Signaling.IsPrivate)
            {
                if (Signaling.ConnectionPair.ContainsKey(connectionId))
                {
                    var pair = Signaling.ConnectionPair[connectionId];
                    var otherSessionWs = pair[0].ID == ID ? pair[1] : pair[0];
                    otherSessionWs?.SendMessage(new { from = connectionId, to = "", type = "candidate", data = candidate });

                }
                return;
            }
            foreach (var kv in Signaling.Clients)
            {
                if (kv.Key != this)
                {
                    kv.Key.SendMessage(new { from = connectionId, to = "", type = "candidate", data = candidate });
                }
            }
        }

    }

    public class WSMessage
    {
        public string type { get; set; }
        public string connectionId { get; set; }
        public WSMessageData data { get; set; }
    }
    public class WSMessageData
    {
        public string connectionId { get; set; }
        public string sdp { get; set; }
        public string candidate { get; set; }
        public int sdpMLineIndex { get; set; }
        public string sdpMid { get; set; }
    }
}