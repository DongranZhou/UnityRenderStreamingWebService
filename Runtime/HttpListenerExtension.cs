using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using WebSocketSharp.Net;

public static class HttpListenerExtension
{

    public static void WriteJson(this HttpListenerResponse rep ,object o)
    {
        string text = JsonConvert.SerializeObject(o);
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        rep.ContentType = "application/json;";
        rep.ContentLength64 = bytes.Length;
        rep.OutputStream.Write(bytes);
        rep.StatusCode = 200;
    }
    public static T ReadBody<T>(this HttpListenerRequest req)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            while (true)
            {
                byte[] bytes = new byte[1024];
                int length = req.InputStream.Read(bytes, 0, bytes.Length);
                if (length == 0)
                    break;
                ms.Write(bytes, 0, length);
            }

            byte[] buffer = ms.ToArray();
            string text = Encoding.UTF8.GetString(buffer);
            return JsonConvert.DeserializeObject<T>(text);
        }
    }
}