# UnityRenderStreaming WebService

## 介绍

- [UnityRenderStreaming / WebApp](https://github.com/Unity-Technologies/UnityRenderStreaming/tree/main/WebApp) 的 C# 版本 ，支持`websocket`,`http`
- 基于 [websocket-sharp](https://github.com/sta/websocket-sharp) 开发

## 注意

- [UnityRenderStreaming](https://github.com/Unity-Technologies/UnityRenderStreaming) 内已包含 [websocket-sharp](https://github.com/sta/websocket-sharp) 否则需要单独安装
- Mono 在进行 `http` 请求时，Content-Length 不得为`空`或`0`， 代码[HttpRequest.MakeInputStream](https://github.com/mono/mono/blob/0f53e9e151d92944cacab3e24ac359410c606df6/mcs/class/System.Web/System.Web/HttpRequest.cs#L819)
  可尝试更改代码。`websocket` 模式不受影响
- [HttpSignaling.cs](https://github.com/Unity-Technologies/UnityRenderStreaming/blob/main/com.unity.renderstreaming/Runtime/Scripts/Signaling/HttpSignaling.cs#L245)  将ContentLength改为1使用

```csharp
private bool HTTPCreate()
{
  HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"{m_url}/signaling");
  request.Method = "PUT";
  request.ContentType = "application/json";
  request.KeepAlive = false;
  //request.ContentLength = 0;
  request.ContentLength = 1; // 改为 1 使用

  RenderStreaming.Logger.Log($"Signaling: Connecting HTTP {m_url}");
  //......
}
```

- [signaling.js](https://github.com/Unity-Technologies/UnityRenderStreaming/blob/main/WebApp/client/src/signaling.js#L30) 添加数据

```javascript
async start() {
  if(this.running) {
    return;
  }

  this.running = true;
  while (!this.sessionId) {
    //const createResponse = await fetch(this.url(''), { method: 'PUT', headers: this.headers() });
    const createResponse = await fetch(this.url(''), { method: 'PUT',body:'{}', headers: this.headers() });//添加空数据
    const session = await createResponse.json();
    this.sessionId = session.sessionId;

    if (!this.sessionId) {
      await this.sleep(this.interval);
    }
  }

  this.loopGetAll();
}
```
