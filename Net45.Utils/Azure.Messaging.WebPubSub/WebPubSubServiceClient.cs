using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Messaging.WebPubSub
{
    public class WebPubSubServiceClient : IDisposable
    {
        public const string SubProtocol = "json.webpubsub.azure.v1";
        private readonly ILogger _logger;
        private readonly string _hubName;
        private readonly string _getUrlApi;
        private readonly HttpClient _httpClient;
        private readonly ClientWebSocket _ws;

        private CancellationTokenSource _receiveCts;
        private Task _circularReceiveTask;

        public event WebPubSubReceiveEventHandler ReceiveAsync;

        public WebPubSubServiceClient(ILoggerFactory loggerFactory, string hubName, string getUrlApi)
        {
            _logger = loggerFactory.CreateLogger($"{nameof(WebPubSubServiceClient)}({hubName})");
            _hubName = hubName;
            _getUrlApi = getUrlApi;
            _httpClient = new HttpClient();
            _ws = new ClientWebSocket();
            _ws.Options.AddSubProtocol(SubProtocol);

            // Azure 的默认最低 TLS 版本为 TLS 1.2 net45必须手动开启
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }

        public async Task ConnectAsync(CancellationToken cancellation = default(CancellationToken))
        {
            cancellation.ThrowIfCancellationRequested();
            using (HttpResponseMessage response = await _httpClient.GetAsync(_getUrlApi, cancellation))
            {
                response.EnsureSuccessStatusCode();
                string url = await response.Content.ReadAsStringAsync();
                cancellation.ThrowIfCancellationRequested();
                await _ws.ConnectAsync(new Uri(url), cancellation);
            }
        }

        public async Task JoinGroupAsync(string group, CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException("group");
            string json = JsonConvert.SerializeObject(new { type = "joinGroup", group });
            await SendAsync(json, cancellation);
            _logger.LogInformation($"Join group[{group}] of hub[{_hubName}]");
        }

        public async Task LeaveGroupAsync(string group, CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException("group");
            string json = JsonConvert.SerializeObject(new { type = "leaveGroup", group });
            await SendAsync(json, cancellation);
            _logger.LogInformation($"Join group[{group}] of hub[{_hubName}]");
        }

        public async Task SendAsync(string content, CancellationToken cancellation = default(CancellationToken))
        {
            if (_ws.State != WebSocketState.Open && _ws.State != WebSocketState.Connecting)
            {
                _logger.LogInformation("WebPubSubServiceClient[{0}] state:{1}", _hubName, _ws.State);
                await ConnectAsync(cancellation);
            }
            ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(content));
            await _ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellation);
            if (_circularReceiveTask == null)
            {
                _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                _circularReceiveTask = Task.Run(async () => await CircularReceiveAsync(_receiveCts.Token), _receiveCts.Token);
            }
        }

        private async Task CircularReceiveAsync(CancellationToken cancellation = default(CancellationToken))
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    if (_ws.State != WebSocketState.Open && _ws.State != WebSocketState.Connecting)
                    {
                        _logger.LogInformation("WebPubSubServiceClient[{0}] state:{1}", _hubName, _ws.State);
                        await ConnectAsync(cancellation);
                    }

                    byte[] buffer = new byte[1024 * 4]; // 缓冲区大小
                    WebSocketReceiveResult result;
                    StringBuilder receivedMessage = new StringBuilder();

                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            string partialMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            receivedMessage.Append(partialMessage);
                        }
                    } while (!result.EndOfMessage);

                    string json = receivedMessage.ToString();
                    _logger.LogInformation("Receive:{0}", json);
                    WebPubSubMessage message = JsonConvert.DeserializeObject<WebPubSubMessage>(json);

                    await ReceiveAsync(this, new WebPubSubReceiveEventArgs(message, cancellation));
                }
                catch (OperationCanceledException) { /*ignore*/ }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
                finally
                {
                    try { await Task.Delay(300, cancellation); } catch { }
                }
            }
        }

        public async Task SendToGroupAsync(string group, string data, CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(group)) throw new ArgumentNullException("group");
            if (string.IsNullOrWhiteSpace(data)) throw new ArgumentNullException("data");
            if (_ws.State != WebSocketState.Open && _ws.State != WebSocketState.Connecting)
            {
                _logger.LogInformation("WebPubSubServiceClient[{0}] state:{1}", _hubName, _ws.State);
                await ConnectAsync(cancellation);
            }
            string json = JsonConvert.SerializeObject(new { type = "sendToGroup", group, data });
            ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
            await _ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellation);
        }

        public async Task CloseAsync(string description = "done", CancellationToken cancellation = default(CancellationToken))
        {
            if (_receiveCts != null)
            {
                _receiveCts.Cancel();
                _receiveCts.Dispose();
                _receiveCts = null;
            }
            if (_circularReceiveTask != null)
            {
                try
                {
                    await _circularReceiveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /*ignore*/ }
                finally
                {
                    _circularReceiveTask.Dispose();
                    _circularReceiveTask = null;
                }
            }
            if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.Connecting)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, description, cancellation);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    CloseAsync().Wait();
                    _ws.Dispose();
                    _httpClient.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~WebPubSubServiceClient() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }

        #endregion
    }
}
