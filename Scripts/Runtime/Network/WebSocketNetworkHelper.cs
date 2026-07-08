using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Network;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// WebSocket 网络辅助器。基于 <see cref="System.Net.WebSockets.ClientWebSocket"/>，
    /// 使用异步接收循环 + 主线程消息分发。
    /// </summary>
    /// <remarks>
    /// WebSocket 原生支持消息分帧，每条 WebSocket 消息即为一个完整帧。
    /// 协议格式：
    /// <code>
    /// [4字节：消息ID（小端序）] [消息体]
    /// </code>
    /// 发送和接收均在同一帧内完成，无需额外的粘包处理。
    /// 适用于 H5 游戏、与 Node.js/Web 服务端通信等场景。
    /// </remarks>
    public class WebSocketNetworkHelper : NetworkHelperBase
    {
        /// <summary>
        /// 消息 ID 字段字节数。
        /// </summary>
        private const int MsgIdSize = 4;

        /// <summary>
        /// WebSocket 客户端实例。
        /// </summary>
        private ClientWebSocket webSocket;

        /// <summary>
        /// 接收取消令牌源。
        /// </summary>
        private CancellationTokenSource receiveCts;

        /// <summary>
        /// "连接"状态。
        /// </summary>
        private volatile bool isConnected;

        /// <summary>
        /// 收到的完整消息队列（接收循环写入，主线程读取）。
        /// </summary>
        private readonly ConcurrentQueue<PendingMessage> pendingMessages = new ConcurrentQueue<PendingMessage>();

        /// <inheritdoc/>
        public override bool IsConnected
        {
            get { return isConnected && webSocket != null && webSocket.State == WebSocketState.Open; }
        }

        /// <inheritdoc/>
        public override async void Connect(string ip, int port)
        {
            if (isConnected)
            {
                return;
            }

            try
            {
                string uri = string.Format("ws://{0}:{1}/", ip, port);
                webSocket = new ClientWebSocket();
                receiveCts = new CancellationTokenSource();

                await webSocket.ConnectAsync(new Uri(uri), CancellationToken.None);
                isConnected = true;

                OnConnected?.Invoke();

                // 启动异步接收循环（fire-and-forget）
                _ = ReceiveLoopAsync(receiveCts.Token);
            }
            catch (Exception ex)
            {
                Log.Error("WebSocketNetworkHelper: Connect failed to {0}:{1}. {2}", ip, port, ex.Message);
                OnError?.Invoke(ex.Message);
            }
        }

        /// <inheritdoc/>
        public override void Disconnect()
        {
            isConnected = false;
            receiveCts?.Cancel();

            try
            {
                if (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
            }
            catch
            {
                // 忽略关闭时的异常
            }

            webSocket?.Dispose();
            webSocket = null;
            receiveCts?.Dispose();
            receiveCts = null;

            OnDisconnected?.Invoke();
        }

        /// <inheritdoc/>
        public override async void Send(byte[] data)
        {
            if (!IsConnected || webSocket == null || data == null)
            {
                return;
            }

            try
            {
                await webSocket.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Binary,
                    true,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Error("WebSocketNetworkHelper: Send failed. {0}", ex.Message);
                isConnected = false;
                OnError?.Invoke(ex.Message);
            }
        }

        /// <inheritdoc/>
        public override void Update()
        {
            // 处理接收队列中的消息（主线程）
            while (pendingMessages.TryDequeue(out PendingMessage msg))
            {
                OnReceive?.Invoke(msg.MsgId, msg.Body);
            }
        }

        /// <inheritdoc/>
        private void OnDestroy()
        {
            Disconnect();
        }

        /// <summary>
        /// 异步接收循环。持续读取 WebSocket 消息直到连接关闭。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested && webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    int totalBytes = 0;

                    // 读取一条完整消息（可能跨多个帧）
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        do
                        {
                            result = await webSocket.ReceiveAsync(
                                new ArraySegment<byte>(buffer), ct);
                            ms.Write(buffer, 0, result.Count);
                            totalBytes += result.Count;
                        }
                        while (!result.EndOfMessage);

                        byte[] data = ms.ToArray();

                        if (data.Length < MsgIdSize)
                        {
                            continue;
                        }

                        // 解析：前 4 字节 msgId，剩余为 body
                        int msgId = BitConverter.ToInt32(data, 0);
                        int bodyLength = data.Length - MsgIdSize;
                        byte[] body = bodyLength > 0 ? new byte[bodyLength] : Array.Empty<byte>();

                        if (bodyLength > 0)
                        {
                            Array.Copy(data, MsgIdSize, body, 0, bodyLength);
                        }

                        pendingMessages.Enqueue(new PendingMessage(msgId, body));
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception)
            {
                if (!ct.IsCancellationRequested)
                {
                    isConnected = false;
                    OnError?.Invoke("WebSocket receive error");
                    OnDisconnected?.Invoke();
                }
            }
        }

        /// <summary>
        /// 待处理的完整消息。
        /// </summary>
        private readonly struct PendingMessage
        {
            /// <summary>
            /// 消息 ID。
            /// </summary>
            public readonly int MsgId;

            /// <summary>
            /// 消息体。
            /// </summary>
            public readonly byte[] Body;

            /// <summary>
            /// 构造待处理消息。
            /// </summary>
            /// <param name="msgId">消息 ID。</param>
            /// <param name="body">消息体。</param>
            public PendingMessage(int msgId, byte[] body)
            {
                MsgId = msgId;
                Body = body;
            }
        }
    }
}
