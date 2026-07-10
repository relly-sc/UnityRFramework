using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using RFramework.Network;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UDP 网络辅助器。基于 <see cref="System.Net.Sockets.UdpClient"/>，
    /// 使用后台线程接收数据报。
    /// </summary>
    /// <remarks>
    /// UDP 是无连接协议，"连接"仅表示记录目标端点。
    /// 每个 UDP 数据报即为一个完整消息：
    /// <code>
    /// [4字节：消息ID（小端序）] [消息体]
    /// </code>
    /// 适用于实时性要求高、可容忍丢包的场景（如位置同步、语音数据）。
    /// </remarks>
    public class UdpNetworkHelper : NetworkHelperBase
    {
        /// <summary>
        /// 消息 ID 字段字节数。
        /// </summary>
        private const int MsgIdSize = 4;

        /// <summary>
        /// UDP 客户端实例。
        /// </summary>
        private UdpClient udpClient;

        /// <summary>
        /// 目标端点。
        /// </summary>
        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// 接收线程。
        /// </summary>
        private Thread receiveThread;

        /// <summary>
        /// "连接"状态（虚拟连接，仅表示是否已设置目标端点）。
        /// </summary>
        private volatile bool isConnected;

        /// <summary>
        /// 接收线程是否应停止。
        /// </summary>
        private volatile bool stopReceive;

        /// <summary>
        /// 收到的完整消息队列（接收线程写入，主线程读取）。
        /// </summary>
        private readonly ConcurrentQueue<PendingMessage> pendingMessages = new ConcurrentQueue<PendingMessage>();

        /// <inheritdoc/>
        public override bool IsConnected
        {
            get { return isConnected; }
        }

        /// <inheritdoc/>
        public override void Connect(string ip, int port)
        {
            if (isConnected)
            {
                return;
            }

            try
            {
                remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                udpClient = new UdpClient();
                udpClient.Connect(remoteEndPoint);

                isConnected = true;
                stopReceive = false;

                // 启动接收线程
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "UdpHelper-Receive"
                };
                receiveThread.Start();

                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("UdpNetworkHelper: Connect failed to {0}:{1}. {2}", ip, port, ex.Message);
                OnError?.Invoke(ex.Message);
            }
        }

        /// <inheritdoc/>
        public override void Disconnect()
        {
            stopReceive = true;
            isConnected = false;

            try
            {
                udpClient?.Close();
            }
            catch
            {
                // 忽略关闭时的异常
            }

            udpClient = null;
            remoteEndPoint = null;

            OnDisconnected?.Invoke();
        }

        /// <inheritdoc/>
        public override void Send(int msgId, byte[] body)
        {
            if (!isConnected || udpClient == null)
            {
                return;
            }

            try
            {
                // 帧协议：[4B msgId][body]
                int bodyLen = body == null ? 0 : body.Length;
                byte[] frame = new byte[MsgIdSize + bodyLen];
                BitConverter.GetBytes(msgId).CopyTo(frame, 0);
                if (bodyLen > 0)
                {
                    body.CopyTo(frame, MsgIdSize);
                }

                udpClient.Send(frame, frame.Length);
            }
            catch (Exception ex)
            {
                Log.Error("UdpNetworkHelper: Send failed. {0}", ex.Message);
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
        /// 接收线程主循环。读取 UDP 数据报并按帧协议拆包。
        /// </summary>
        private void ReceiveLoop()
        {
            while (!stopReceive && isConnected && udpClient != null)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpClient.Receive(ref sender);

                    if (data == null || data.Length < MsgIdSize)
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
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    if (!stopReceive)
                    {
                        isConnected = false;
                        OnError?.Invoke("Receive error");
                        OnDisconnected?.Invoke();
                    }
                    break;
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
