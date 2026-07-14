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
        public override void Connect(string ip, int port)
        {
            if (isConnected)
            {
                return;
            }

            _ = ConnectAsync(ip, port);
        }

        private async Task ConnectAsync(string ip, int port)
        {
            ClientWebSocket socket = new ClientWebSocket();
            CancellationTokenSource cts = new CancellationTokenSource();
            try
            {
                string uri = string.Format("ws://{0}:{1}/", ip, port);
                webSocket = socket;
                receiveCts = cts;

                await socket.ConnectAsync(new Uri(uri), cts.Token);
                if (!ReferenceEquals(webSocket, socket))
                {
                    return;
                }

                isConnected = true;

                OnConnected?.Invoke();

                _ = ReceiveLoopAsync(socket, cts.Token);
            }
            catch (Exception ex)
            {
                if (!ReferenceEquals(webSocket, socket))
                {
                    return;
                }

                webSocket = null;
                receiveCts = null;
                cts.Dispose();
                socket.Dispose();
                Log.Error("WebSocketNetworkHelper: Connect failed to {0}:{1}. {2}", ip, port, ex.Message);
                OnError?.Invoke(ex.Message);
            }
        }

        /// <inheritdoc/>
        public override void Disconnect()
        {
            ClientWebSocket socket = webSocket;
            CancellationTokenSource cts = receiveCts;
            bool wasConnected = isConnected;
            isConnected = false;
            webSocket = null;
            receiveCts = null;
            cts?.Cancel();
            _ = CloseAndDisposeAsync(socket, cts);

            if (wasConnected)
            {
                OnDisconnected?.Invoke();
            }
        }

        /// <inheritdoc/>
        public override void Send(int msgId, byte[] body)
        {
            ClientWebSocket socket = webSocket;
            if (!IsConnected || socket == null)
            {
                return;
            }

            _ = SendAsync(socket, msgId, body);
        }

        private async Task SendAsync(ClientWebSocket socket, int msgId, byte[] body)
        {
            try
            {
                // 帧协议：[4B msgId][body]（WebSocket 原生分帧承载整条消息）
                int bodyLen = body == null ? 0 : body.Length;
                byte[] frame = new byte[MsgIdSize + bodyLen];
                BitConverter.GetBytes(msgId).CopyTo(frame, 0);
                if (bodyLen > 0)
                {
                    body.CopyTo(frame, MsgIdSize);
                }

                await socket.SendAsync(
                    new ArraySegment<byte>(frame),
                    WebSocketMessageType.Binary,
                    true,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (!ReferenceEquals(webSocket, socket))
                {
                    return;
                }

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
        private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
        {
            // 单条消息最大长度（8MB），防御恶意/异常的大消息导致内存暴涨
            const int MaxMessageSize = 8 * 1024 * 1024;
            byte[] buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct);

                    // Close 帧优先处理：不按业务消息解析，触发断开回调后退出循环
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        EndConnection(socket);
                        break;
                    }

                    // 读取一条完整消息（可能跨多个帧）
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        // 先写入首个分片（外层已接收）。
                        // 若首个分片即为消息结尾（单帧消息），则不会进入续收循环，
                        // 避免无谓地多等一条消息、进而吞掉后续消息或令单帧消息永久阻塞。
                        ms.Write(buffer, 0, result.Count);

                        if (ms.Length > MaxMessageSize)
                        {
                            throw new InvalidOperationException(
                                "WebSocket message exceeds max allowed size.");
                        }

                        while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close)
                        {
                            result = await socket.ReceiveAsync(
                                new ArraySegment<byte>(buffer), ct);

                            // 续收阶段出现 Close 帧：放弃本条消息，按断开处理
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                EndConnection(socket);
                                break;
                            }

                            ms.Write(buffer, 0, result.Count);

                            if (ms.Length > MaxMessageSize)
                            {
                                throw new InvalidOperationException(
                                    "WebSocket message exceeds max allowed size.");
                            }
                        }

                        // 续收阶段收到 Close 帧：已触发断开回调，直接退出外层循环
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

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
                    OnError?.Invoke("WebSocket receive error");
                    EndConnection(socket);
                }
            }
        }

        private void EndConnection(ClientWebSocket socket)
        {
            if (!ReferenceEquals(webSocket, socket))
            {
                return;
            }

            bool wasConnected = isConnected;
            CancellationTokenSource cts = receiveCts;
            isConnected = false;
            webSocket = null;
            receiveCts = null;
            cts?.Cancel();
            _ = CloseAndDisposeAsync(socket, cts);

            if (wasConnected)
            {
                OnDisconnected?.Invoke();
            }
        }

        private static async Task CloseAndDisposeAsync(ClientWebSocket socket, CancellationTokenSource cts)
        {
            try
            {
                if (socket != null && socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
                }
            }
            catch
            {
                // Closing a socket that is already aborted is expected.
            }
            finally
            {
                socket?.Dispose();
                cts?.Dispose();
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
