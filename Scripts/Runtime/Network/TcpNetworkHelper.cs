using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Network;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// TCP 网络辅助器。基于 <see cref="System.Net.Sockets.TcpClient"/>，
    /// 使用后台线程接收数据 + 长度前缀帧协议。
    /// </summary>
    /// <remarks>
    /// 协议格式（小端序）：
    /// <code>
    /// [4字节：总长度（含自身）] [4字节：消息ID] [消息体]
    /// </code>
    /// 包长度字段包含自身 4 字节，即最小有效包为 8 字节（空消息体）。
    /// </remarks>
    public class TcpNetworkHelper : NetworkHelperBase
    {
        /// <summary>
        /// 包长度字段字节数。
        /// </summary>
        private const int PacketLengthSize = 4;

        /// <summary>
        /// 消息 ID 字段字节数。
        /// </summary>
        private const int MsgIdSize = 4;

        /// <summary>
        /// 单条数据包最大长度（16MB），防御恶意/异常的大包长度字段触发巨大内存分配。
        /// </summary>
        private const int MaxPacketLength = 16 * 1024 * 1024;

        /// <summary>
        /// TCP 客户端实例。
        /// </summary>
        private TcpClient tcpClient;

        /// <summary>
        /// 网络流。
        /// </summary>
        private NetworkStream stream;

        /// <summary>
        /// 接收线程。
        /// </summary>
        private Thread receiveThread;

        /// <summary>
        /// 连接状态（线程安全）。
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

            _ = ConnectAsync(ip, port);
        }

        private async Task ConnectAsync(string ip, int port)
        {
            TcpClient client = new TcpClient();
            try
            {
                client.NoDelay = true;
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 3000;
                tcpClient = client;
                await client.ConnectAsync(ip, port);

                // Disconnect can dispose and detach the client while the
                // operating-system connect attempt is still completing.
                if (!ReferenceEquals(tcpClient, client))
                {
                    client.Close();
                    return;
                }

                stream = client.GetStream();
                isConnected = true;
                stopReceive = false;

                // 启动接收线程
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "TcpHelper-Receive"
                };
                receiveThread.Start();

                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                if (!ReferenceEquals(tcpClient, client))
                {
                    client.Close();
                    return;
                }

                tcpClient = null;
                client.Close();
                Log.Error("TcpNetworkHelper: Connect failed to {0}:{1}. {2}", ip, port, ex.Message);
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
                stream?.Close();
                tcpClient?.Close();
            }
            catch
            {
                // 忽略关闭时的异常
            }

            stream = null;
            tcpClient = null;

            OnDisconnected?.Invoke();
        }

        /// <inheritdoc/>
        public override void Send(int msgId, byte[] body)
        {
            if (!isConnected || stream == null)
            {
                return;
            }

            try
            {
                // 帧协议：[4B 包长（含自身）][4B msgId][body]
                int bodyLen = body == null ? 0 : body.Length;
                int totalLen = PacketLengthSize + MsgIdSize + bodyLen;
                byte[] frame = new byte[totalLen];
                BitConverter.GetBytes(totalLen).CopyTo(frame, 0);
                BitConverter.GetBytes(msgId).CopyTo(frame, PacketLengthSize);
                if (bodyLen > 0)
                {
                    body.CopyTo(frame, PacketLengthSize + MsgIdSize);
                }

                stream.Write(frame, 0, frame.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Log.Error("TcpNetworkHelper: Send failed. {0}", ex.Message);
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
        /// 接收线程主循环。读取网络流并按长度前缀帧协议拆包。
        /// </summary>
        private void ReceiveLoop()
        {
            byte[] lengthBuffer = new byte[PacketLengthSize];

            while (!stopReceive && isConnected && stream != null)
            {
                try
                {
                    // 读取包长度（4 字节）
                    if (!ReadExactly(lengthBuffer, 0, PacketLengthSize))
                    {
                        break;
                    }

                    int packetLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (packetLength < PacketLengthSize + MsgIdSize || packetLength > MaxPacketLength)
                    {
                        // 非法长度字段无法可靠定位载荷边界，若仅 continue 会把载荷字节
                        // 误读为下一条包长从而导致协议流永久失步，因此直接断开连接。
                        Log.Warning("TcpNetworkHelper: Invalid packet length: {0}, disconnect.", packetLength);
                        isConnected = false;
                        Disconnect();
                        break;
                    }

                    int remaining = packetLength - PacketLengthSize;
                    byte[] packetBuffer = new byte[remaining];

                    if (!ReadExactly(packetBuffer, 0, remaining))
                    {
                        break;
                    }

                    // 解析：前 4 字节 msgId，剩余为 body
                    int msgId = BitConverter.ToInt32(packetBuffer, 0);
                    int bodyLength = remaining - MsgIdSize;
                    byte[] body = bodyLength > 0 ? new byte[bodyLength] : Array.Empty<byte>();

                    if (bodyLength > 0)
                    {
                        Array.Copy(packetBuffer, MsgIdSize, body, 0, bodyLength);
                    }

                    pendingMessages.Enqueue(new PendingMessage(msgId, body));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
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
        /// 从网络流中精确读取指定字节数。
        /// </summary>
        /// <param name="buffer">目标缓冲区。</param>
        /// <param name="offset">写入偏移。</param>
        /// <param name="count">需要读取的字节数。</param>
        /// <returns>读取成功返回 true，连接断开返回 false。</returns>
        private bool ReadExactly(byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int bytesRead = stream.Read(buffer, offset + read, count - read);
                if (bytesRead <= 0)
                {
                    return false;
                }

                read += bytesRead;
            }

            return true;
        }

        /// <summary>
        /// 待处理的完整消息。接收线程填充，主线程消费。
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
