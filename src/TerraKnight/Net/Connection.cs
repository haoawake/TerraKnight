using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace TerraKnight.Net
{
    /// <summary>
    /// One TCP connection (Terraria's TcpSocket/RemoteClient): a thread reads whole messages into a
    /// queue, another writes what is sent, in order, so neither the game nor the server loop ever
    /// waits on the network. Closing drops it at once; <see cref="CloseAfterSending"/> lets what is
    /// queued go first (a kick's reason).
    /// </summary>
    internal sealed class Connection
    {
        readonly TcpClient tcp;
        readonly NetworkStream stream;
        readonly ConcurrentQueue<byte[]> inbox = new ConcurrentQueue<byte[]>();
        readonly BlockingCollection<byte[]> outbox = new BlockingCollection<byte[]>();
        volatile bool closed;
        long lastReceived;

        /// <summary>Why it closed (a lost connection, a kick, the other end leaving), in the mod's Chinese: shown through Lang.T on the game's thread.</summary>
        public string CloseReason { get; private set; }
        public bool Closed => closed;
        public string Remote { get; }
        /// <summary>Bytes in and out so far.</summary>
        public long BytesIn, BytesOut;
        /// <summary>Bytes waiting to be written, and the messages left out because too much was waiting.</summary>
        long queued;
        public long Queued => Interlocked.Read(ref queued);
        public int Dropped;

        /// <summary>
        /// How much may wait to go out before what a later message replaces anyway is left out: a player's state, an
        /// owner's enemy and scene snapshots, projectiles and minions, their dust and numbers. The rest (the world, chat,
        /// deaths, hits, tiles, a seal) always goes. A slow link then falls behind by a moment instead of without end,
        /// with ever more memory held for it.
        /// </summary>
        const long MaxQueued = 192 * 1024;

        static bool Superseded(byte[] packet)
        {
            if (packet.Length < 3) return false;
            switch ((Msg)packet[2])
            {
                case Msg.PlayerState:
                case Msg.EnemySnapshot:
                case Msg.ProjectileSnapshot:
                case Msg.MinionSnapshot:
                case Msg.EnemyAttacks:
                case Msg.NetFx:
                case Msg.SceneMirror:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Seconds since the last message came in.</summary>
        public double Silence => (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastReceived)) / (double)TimeSpan.TicksPerSecond;

        public Connection(TcpClient client)
        {
            tcp = client;
            tcp.NoDelay = true;
            stream = tcp.GetStream();
            try { Remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?"; }
            catch (Exception) { Remote = "?"; }
            lastReceived = DateTime.UtcNow.Ticks;
            new Thread(ReadLoop) { IsBackground = true, Name = "TerraKnight net read " + Remote }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "TerraKnight net write " + Remote }.Start();
        }

        public void Send(byte[] packet)
        {
            if (closed || packet == null) return;
            if (Interlocked.Read(ref queued) > MaxQueued && Superseded(packet)) { Interlocked.Increment(ref Dropped); return; }
            Interlocked.Add(ref queued, packet.Length);
            try { outbox.Add(packet); }
            catch (InvalidOperationException) { Interlocked.Add(ref queued, -packet.Length); }
        }

        /// <summary>The next whole message received (its type and fields), if one is waiting.</summary>
        public bool TryReceive(out byte[] body) => inbox.TryDequeue(out body);

        void ReadLoop()
        {
            var header = new byte[2];
            try
            {
                while (!closed)
                {
                    if (!ReadExactly(header, 2)) break;
                    int length = header[0] | header[1] << 8;
                    if (length < 3) throw new InvalidOperationException("消息长度不对：" + length);
                    var body = new byte[length - 2];
                    if (!ReadExactly(body, body.Length)) break;
                    Interlocked.Add(ref BytesIn, length);
                    Interlocked.Exchange(ref lastReceived, DateTime.UtcNow.Ticks);
                    inbox.Enqueue(body);
                }
                Close("对方断开了连接");
            }
            catch (Exception e)
            {
                Close(closed ? CloseReason : e is System.IO.IOException ? "连接已丢失" : "连接已丢失：" + e.Message);
            }
        }

        bool ReadExactly(byte[] buffer, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = stream.Read(buffer, got, count - got);
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        void WriteLoop()
        {
            try
            {
                foreach (var p in outbox.GetConsumingEnumerable())
                {
                    stream.Write(p, 0, p.Length);
                    Interlocked.Add(ref BytesOut, p.Length);
                    Interlocked.Add(ref queued, -p.Length);
                }
                stream.Flush();
            }
            catch (Exception)
            {
                Close(CloseReason ?? "连接已丢失");
            }
            // A graceful close: everything sent, then the socket goes.
            closed = true;
            try { tcp.Close(); }
            catch (Exception) { }
        }

        /// <summary>Lets what is queued go out, then closes.</summary>
        public void CloseAfterSending(string reason)
        {
            if (closed) return;
            CloseReason = reason;
            try { outbox.CompleteAdding(); }
            catch (Exception) { }
        }

        public void Close(string reason)
        {
            if (closed) return;
            closed = true;
            CloseReason = reason;
            try { outbox.CompleteAdding(); }
            catch (Exception) { }
            try { tcp.Close(); }
            catch (Exception) { }
        }
    }
}
