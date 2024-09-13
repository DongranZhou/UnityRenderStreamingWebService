using Codice.Client.BaseCommands;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.MemoryProfiler;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using WebSocketSharp.Net;
using static Codice.Client.BaseCommands.Import.Commit;
using static DG.DemiEditor.DeEditorUtils;
using static log4net.Appender.RollingFileAppender;
using static PlasticPipe.Server.ConnectionFromClientList;

public class HTTPSignaling
{
    public bool IsPrivate { get; set; }
    public int TimeoutRequestedTime { get; set; } = 10000;
    public Dictionary<string, List<string>> Clients { get; private set; } = new Dictionary<string, List<string>>();
    public Dictionary<string, long> LastRequestedTime { get; private set; } = new Dictionary<string, long>();
    public Dictionary<string, string[]> ConnectionPair { get; private set; } = new Dictionary<string, string[]>();
    public Dictionary<string, Dictionary<string, Offer>> Offers { get; private set; } = new Dictionary<string, Dictionary<string, Offer>>();
    public Dictionary<string, Dictionary<string, Answer>> Answers { get; private set; } = new Dictionary<string, Dictionary<string, Answer>>();
    public Dictionary<string, Dictionary<string, List<Candidate>>> Candidates { get; private set; } = new Dictionary<string, Dictionary<string, List<Candidate>>>();
    public Dictionary<string, List<Disconnection>> Disconnections { get; private set; } = new Dictionary<string, List<Disconnection>>();


    List<string> GetOrCreateConnectionIds(string sessionId)
    {
        if (!Clients.ContainsKey(sessionId))
            Clients[sessionId] = new List<string>();
        return Clients[sessionId];
    }

    public bool CheckSessionId(HttpListenerRequest req,HttpListenerResponse rep)
    {
        if (req.RawUrl == "/signaling")
            return true;
        string id = req.Headers.Get("session-id");
        if (string.IsNullOrEmpty(id) || !Clients.ContainsKey(id))
            return false;
        LastRequestedTime[id] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return true;
    }

    public void GetAnswer(HttpListenerRequest req, HttpListenerResponse rep)
    {
        var fromTime = long.TryParse(req.QueryString.Get("fromtime"), out long fromtimeValue) ? fromtimeValue : 0;
        var sessionId = req.Headers.Get("session-id");
        var answers = _GetAnswer(sessionId, fromTime);
        rep.WriteJson(new { answers = answers.Select((v) => (new { connectionId = v.Key, sdp= v.Value.sdp, type= "answer", datetime= v.Value.datetime })) });
    }
    public void GetConnection(HttpListenerRequest req, HttpListenerResponse rep) 
    {
        string sessionId = req.Headers.Get("session-id");
        List<string> connections = _GetConnection(sessionId);
        rep.WriteJson(new { connections = connections.Select(x => new { connectionId = x, type = "connect", datetime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) });
    }
    public void GetOffer(HttpListenerRequest req, HttpListenerResponse rep) 
    {
        var fromTime = long.TryParse(req.QueryString.Get("fromtime"),out long fromtimeValue) ? fromtimeValue : 0;
        var sessionId = req.Headers.Get("session-id");
        var offers = _GetOffer(sessionId, fromTime);
        rep.WriteJson(new { offers= offers.Select(v => new { connectionId = v.Key, sdp = v.Value.sdp, polite= v.Value.polite, type= "offer", datetime= v.Value.datetime }) });
    }
    public void GetCandidate(HttpListenerRequest req, HttpListenerResponse rep)
    {
        var fromTime = long.TryParse(req.QueryString.Get("fromtime"), out long fromtimeValue) ? fromtimeValue : 0;
        var sessionId = req.Headers.Get("session-id");
        var candidates = _GetCandidate(sessionId, fromTime);
        rep.WriteJson(new { candidates = candidates.Select((v) => new { connectionId = v.Key, candidate = v.Value.candidate, sdpMLineIndex = v.Value.sdpMLineIndex, sdpMid = v.Value.sdpMid, type = "candidate", datetime = v.Value.datetime }) });
    }
    public void GetAll(HttpListenerRequest req, HttpListenerResponse rep) 
    {
        var fromTime = long.TryParse(req.QueryString.Get("fromtime"), out long fromtimeValue) ? fromtimeValue : 0;
        var sessionId = req.Headers.Get("session-id");
        var connections = _GetConnection(sessionId);
        var offers = _GetOffer(sessionId, fromTime);
        var answers = _GetAnswer(sessionId, fromTime);
        var candidates = _GetCandidate(sessionId, fromTime);
        var disconnections = _getDisconnection(sessionId, fromTime);
        var datetime = LastRequestedTime[sessionId];
        var array = new List<SignalingMessage>();

        array.AddRange(connections.Select((v) => new SignalingMessage { connectionId= v, type= "connect", datetime= datetime }));
        array.AddRange(offers.Select((v) => (new SignalingMessage { connectionId= v.Key, sdp= v.Value.sdp, polite= v.Value.polite, type= "offer", datetime= v.Value.datetime })));
        array.AddRange(answers.Select((v) => (new SignalingMessage { connectionId= v.Key, sdp= v.Value.sdp, type= "answer", datetime= v.Value.datetime })));
        array.AddRange(candidates.Select((v) => (new SignalingMessage { connectionId= v.Key, candidate= v.Value.candidate, sdpMLineIndex= v.Value.sdpMLineIndex, sdpMid= v.Value.sdpMid, type= "candidate", datetime= v.Value.datetime })));
        array.AddRange(disconnections.Select((v) => (new SignalingMessage { connectionId= v.id, type= "disconnect", datetime= v.datetime })));

        array = array.OrderBy((a) => a.datetime).ToList();
        rep.WriteJson(new { messages = array, datetime = datetime });
    }
    public void CreateSession(HttpListenerRequest req, HttpListenerResponse rep)
    {
        var sessionId = Guid.NewGuid().ToString();
        CreateSession(sessionId, rep);
    }
    public void CreateSession(string sessionId, HttpListenerResponse rep)
    {
        Clients[sessionId] = new List<string>();
        Offers[sessionId] = new Dictionary<string, Offer>();
        Answers[sessionId] = new Dictionary<string, Answer>();
        Candidates[sessionId] = new Dictionary<string, List<Candidate>>();
        Disconnections[sessionId] = new List<Disconnection>();
        rep.WriteJson(new { sessionId = sessionId });
    }

    public void CreateConnection(HttpListenerRequest req, HttpListenerResponse rep)
    {
        var sessionId = req.Headers.Get("session-id");
        var fromTime = long.TryParse(req.QueryString.Get("fromtime"), out long fromtimeValue) ? fromtimeValue : 0;
        Dictionary<string,string> body = req.ReadBody<Dictionary<string, string>>();
        var connectionId = body["connectionId"];
        var datetime = LastRequestedTime[sessionId];

        if (string.IsNullOrEmpty(connectionId))
        {
            rep.StatusCode = 400;
            rep.StatusDescription = JsonConvert.SerializeObject(new { error= $"{connectionId} is required" });
            return;
        }
        var polite = true;
        if (IsPrivate)
        {
            if (ConnectionPair.ContainsKey(connectionId))
            {
                var pair = ConnectionPair[connectionId];

                if (pair[0] != null && pair[1] != null)
                {
                    rep.StatusCode = 400;
                    rep.StatusDescription = JsonConvert.SerializeObject(new { error = $"{ connectionId }: This connection id is already used." });
                    return;
                }
                else if (pair[0] != null)
                {
                    ConnectionPair[connectionId] = new string[] { pair[0], sessionId };
                    var map = GetOrCreateConnectionIds(pair[0]);
                    if(!map.Contains(connectionId))
                        map.Add(connectionId);
                }
            }
            else
            {
                ConnectionPair[connectionId] = new string[] { sessionId, null };
                polite = false;
            }
        }

        var connectionIds = GetOrCreateConnectionIds(sessionId);
        if(!connectionIds.Contains(connectionId))
            connectionIds.Add(connectionId);
        
        rep.WriteJson(new { connectionId= connectionId, polite= polite, type= "connect", datetime= datetime });
    }
    public void DeleteSession(HttpListenerRequest req, HttpListenerResponse rep) 
    {
        var sessionId = req.Headers.Get("session-id");
        _DeleteSession(sessionId);
        rep.StatusCode = 200;
    }
    public void DeleteConnection(HttpListenerRequest req, HttpListenerResponse rep)
    {
        var sessionId = req.Headers.Get("session-id");
        Dictionary<string, string> body = req.ReadBody<Dictionary<string, string>>();
        string connectionId = body["connectionId"];
        var datetime = LastRequestedTime[sessionId];
        _DeleteConnection(sessionId, connectionId, datetime);
        rep.WriteJson(new { connectionId= connectionId });
    }
    public void PostOffer(HttpListenerRequest req, HttpListenerResponse rep) 
    {
        var sessionId = req.Headers.Get("session-id");
        Dictionary<string, string> body = req.ReadBody<Dictionary<string, string>>();
        string connectionId = body["connectionId"];
        string sdp = body["sdp"];
        var datetime = LastRequestedTime[sessionId];
        var keySessionId = "";
        var polite = false;

        if (IsPrivate)
        {
            if (ConnectionPair.ContainsKey(connectionId))
            {
                var pair = ConnectionPair[connectionId];
                keySessionId = pair[0] == sessionId ? pair[1] : pair[0];
                if (keySessionId != null)
                {
                    polite = true;
                    var map = Offers[keySessionId];
                    map[connectionId] = new Offer(sdp, datetime, polite);
                }
            }
            rep.StatusCode = 200;
            return;
        }

        {
            if (!ConnectionPair.ContainsKey(connectionId))
            {
                ConnectionPair[connectionId] = new string[] { sessionId, null };
            }

            keySessionId = sessionId;
            var map = Offers[keySessionId];
            map[connectionId] = new Offer(sdp, datetime, polite);

            rep.StatusCode = 200;
        }
    }
    public void PostAnswer(HttpListenerRequest req, HttpListenerResponse rep)
    {
        var sessionId = req.Headers.Get("session-id");
        Dictionary<string, string> body = req.ReadBody<Dictionary<string, string>>();
        string connectionId = body["connectionId"];
        string sdp = body["sdp"];
        var datetime = LastRequestedTime[sessionId];
        var connectionIds = GetOrCreateConnectionIds(sessionId);
        if(!connectionIds.Contains(connectionId))
            connectionIds.Add(connectionId);

        if (!ConnectionPair.ContainsKey(connectionId))
        {
            rep.StatusCode = 200;
            return;
        }

        // add connectionPair
        var pair = ConnectionPair[connectionId];
        var otherSessionId = pair[0] == sessionId ? pair[1] : pair[0];
        if (!Clients.ContainsKey(otherSessionId))
        {
            // already deleted
            rep.StatusCode = 200;
            return;
        }

        if (!IsPrivate)
        {
            ConnectionPair[connectionId] = new string[] { otherSessionId, sessionId };
        }

        var map = Answers[otherSessionId];
        map[connectionId] = new Answer(sdp, datetime);

        // update datetime for candidates
        var mapCandidates = Candidates[otherSessionId];
        if (mapCandidates != null)
        {
            var arrayCandidates = mapCandidates[connectionId];
            if (arrayCandidates != null)
            {
                foreach (var candidate in arrayCandidates) {
                    candidate.datetime = datetime;
                }
            }
        }
        rep.StatusCode = 200;
    }
    public void PostCandidate(HttpListenerRequest req, HttpListenerResponse rep)
    {
        var sessionId = req.Headers.Get("session-id");
        SignalingMessage body = req.ReadBody<SignalingMessage>();
        string connectionId = body.connectionId;
        var datetime = LastRequestedTime[sessionId];

        var map = Candidates[sessionId];
        if (!map.ContainsKey(connectionId))
        {
            map[connectionId] = new List<Candidate>();
        }
        var arr = map[connectionId];
        var candidate = new Candidate(body.candidate, body.sdpMLineIndex, body.sdpMid, datetime);
        arr.Add(candidate);
        rep.StatusCode = 200;
     }

    List<string> _GetConnection(string sessionId) 
    {
      _CheckForTimedOutSessions();
      return Clients[sessionId];
    }

    void _CheckForTimedOutSessions()
    {
        string[] keys = Clients.Keys.ToArray();
        Clients.Keys.CopyTo(keys,0);
        foreach (var sessionId in keys)
        {
            if(!LastRequestedTime.ContainsKey(sessionId))
              continue;
            if(LastRequestedTime[sessionId] > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - TimeoutRequestedTime)
              continue;
            _DeleteSession(sessionId);
        }
    }

    void _DeleteSession(string sessionId) {
        if (Clients.ContainsKey(sessionId))
        {
            foreach (var connectionId in Clients[sessionId]) 
            {
                _DeleteConnection(sessionId, connectionId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }
        Offers.Remove(sessionId);
        Answers.Remove(sessionId);
        Candidates.Remove(sessionId);
        Clients.Remove(sessionId);
        Disconnections.Remove(sessionId);
    }

    void _DeleteConnection(string sessionId,string connectionId,long datetime)
    {
        Clients[sessionId].Remove(connectionId);

        if (IsPrivate)
        {
            if (ConnectionPair.ContainsKey(connectionId))
            {
                var pair = ConnectionPair[connectionId];
                var otherSessionId = pair[0] == sessionId ? pair[1] : pair[0];
                if (!string.IsNullOrEmpty(otherSessionId))
                {
                    if (Clients.ContainsKey(otherSessionId))
                    {
                        Clients[otherSessionId].Remove(connectionId);
                        var array1 = Disconnections[otherSessionId];
                        array1.Add(new Disconnection(connectionId, datetime));
                    }
                }
            }
        }
        else
        {
            foreach (var kv in Disconnections)
            {
                if (kv.Key == sessionId)
                    return;
                kv.Value.Add(new Disconnection(connectionId, datetime));
            }
        }

        ConnectionPair.Remove(connectionId);
        Offers[sessionId].Remove(connectionId);
        Answers[sessionId].Remove(connectionId);
        Candidates[sessionId].Remove(connectionId);

        var array2 = Disconnections[sessionId];
        array2.Add(new Disconnection(connectionId, datetime));
    }

    List<KeyValuePair<string, Offer>> _GetOffer(string sessionId,long fromTime) 
    {
        var arrayOffers = new List<KeyValuePair<string,Offer>>();

        if (Offers.Count != 0)
        {
            if (IsPrivate)
            {
                if (Offers.ContainsKey(sessionId))
                {
                    arrayOffers = Offers[sessionId].ToList();
                }
            }
            else
            {
                var otherSessionMap = Offers.Where(x=>x.Key != sessionId).Select(x=>x.Value).ToList();
                foreach (var otherSession in otherSessionMap)
                    arrayOffers.AddRange(otherSession);
            }
        }

        if (fromTime > 0)
        {
            arrayOffers = arrayOffers.Where((v) => v.Value.datetime >= fromTime).ToList();
        }
        return arrayOffers;
    }

    List<KeyValuePair<string, Answer>> _GetAnswer(string sessionId, long fromTime) 
    {
        List<KeyValuePair<string, Answer>> arrayAnswers = new List<KeyValuePair<string, Answer>>();

        if (Answers.Count != 0 && Answers.ContainsKey(sessionId))
        {
            arrayAnswers = Answers[sessionId].ToList();
        }

        if (fromTime > 0)
        {
            arrayAnswers = arrayAnswers.Where((v) => v.Value.datetime >= fromTime).ToList();
        }
        return arrayAnswers;
    }
    List<KeyValuePair<string, Candidate>> _GetCandidate(string sessionId, long fromTime)
    {
        var connectionIds = Clients[sessionId];
        var arr = new List<KeyValuePair<string, Candidate>>();
        foreach (var connectionId in connectionIds) {
            var pair = ConnectionPair[connectionId];
            if (pair == null)
            {
                continue;
            }
            var otherSessionId = sessionId == pair[0] ? pair[1] : pair[0];
            if (Candidates[otherSessionId] != null || Candidates[otherSessionId][connectionId] != null)
            {
                continue;
            }
            var arrayCandidates = Candidates[otherSessionId][connectionId].Where((v) => v.datetime >= fromTime).ToList();
            if (arrayCandidates.Count == 0)
            {
                continue;
            }
            foreach (var candidate in arrayCandidates) 
            {
                arr.Add(new KeyValuePair<string, Candidate>(connectionId, candidate));
            }
        }
        return arr;
    }
    List<Disconnection> _getDisconnection(string sessionId, long fromTime)
    {
        _CheckForTimedOutSessions();
        var arrayDisconnections = new List<Disconnection>();
        if (Disconnections.Count != 0 && Disconnections.ContainsKey(sessionId))
        {
            arrayDisconnections = Disconnections[sessionId];
        }
        if (fromTime > 0)
        {
            arrayDisconnections = arrayDisconnections.Where((v) => v.datetime >= fromTime).ToList();
        }
        return arrayDisconnections;
    }
}

public class SignalingMessage 
{
    public string connectionId {  get; set; }
    public string candidate { get; set; }
    public int sdpMLineIndex { get; set; }
    public string sdpMid {  get; set; }
    public string type { get; set; }
    public long datetime {  get; set; }
    public string sdp {  get; set; }
    public bool polite {  get; set; }
}