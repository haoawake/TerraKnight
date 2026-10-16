using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// The server of a LAN game, run on its own thread inside the host's game as Terraria's "Host &amp;
    /// Play" runs one beside it: it listens on a port (7777 by default), lets a player in as Terraria
    /// does (Hello with the version, then the password if there is one, then a slot, then the
    /// player's data: MessageBuffer cases 1, 37, 38, 3, 4), tells everyone who came and went, and
    /// passes the players' messages on. The host's own game joins it through the loopback like
    /// anyone else. It decides two things itself: chat commands (ChatCommandProcessor's /p, /me,
    /// /roll, /playing, /death...) and which player runs each scene's enemies (the first one in it,
    /// as long as they stay; see <see cref="Enemies"/>).
    /// </summary>
    internal sealed class Server
    {
        public const int DefaultPort = 7777;
        public const int DefaultMaxPlayers = 8;
        public const int MaxSlots = 16;
        /// <summary>What a client must say in its Hello (Terraria: "Terraria" + curRelease).</summary>
        public static string Protocol => "TerraKnight" + Plugin.Version + "/net" + NetVersion;
        const int NetVersion = 3;
        /// <summary>RemoteClient's timeout: two minutes without a word (a scene loading on the other end can stall it for seconds).</summary>
        const double TimeoutSeconds = 120;

        sealed class Client
        {
            public Connection Conn;
            public int Slot;
            /// <summary>0 just connected; -1 asked for the password; 1 given its slot; 2 in the game.</summary>
            public int State;
            public bool IsHost;
            public string Name = "";
            public byte[] Info, LastState, LastLook;
            public bool Pvp;
            public byte Team;
            /// <summary>The scenes the player is in (the current one, a boss room loaded with it) and since when; every scene they have loaded.</summary>
            public readonly Dictionary<string, long> In = new Dictionary<string, long>();
            public readonly HashSet<string> Loaded = new HashSet<string>();
            public int Deaths, PvpDeaths;
        }

        readonly Client[] clients = new Client[MaxSlots];
        readonly Dictionary<string, int> owners = new Dictionary<string, int>();
        readonly TcpListener listener;
        readonly string password;
        readonly int maxPlayers;
        volatile bool stop;
        long tick;

        public int Port { get; }
        /// <summary>The host's own client says this in its Hello: its world time is everyone's.</summary>
        public readonly string HostKey = Guid.NewGuid().ToString("N");
        public int PlayerCount { get; private set; }

        Server(TcpListener listener, int port, string password, int maxPlayers)
        {
            this.listener = listener;
            Port = port;
            this.password = password ?? "";
            this.maxPlayers = Mathf.Clamp(maxPlayers, 1, MaxSlots);
        }

        /// <summary>Starts listening on every interface; null (and why) when the port can't be had.</summary>
        public static Server Start(int port, string password, int maxPlayers, out string error)
        {
            error = null;
            TcpListener l;
            try
            {
                l = new TcpListener(IPAddress.Any, port);
                l.Start();
            }
            catch (Exception e)
            {
                error = Lang.F("启动服务器失败：端口 {0} 无法使用（{1}）", port, e.Message);
                return null;
            }
            var s = new Server(l, port, password, maxPlayers);
            new Thread(s.Loop) { IsBackground = true, Name = "TerraKnight server" }.Start();
            Plugin.Log.LogInfo($"[联机] 服务器已启动，端口 {port}，最多 {s.maxPlayers} 人，{(string.IsNullOrEmpty(password) ? "无密码" : "有密码")}");
            return s;
        }

        public void Stop() => stop = true;

        void Loop()
        {
            while (!stop)
            {
                try
                {
                    tick++;
                    while (listener.Pending()) Accept(listener.AcceptTcpClient());
                    for (int i = 0; i < clients.Length; i++)
                    {
                        var c = clients[i];
                        if (c == null) continue;
                        for (int n = 0; n < 400 && clients[i] == c && c.Conn.TryReceive(out var body); n++)
                        {
                            try { Handle(c, body); }
                            catch (Exception e) { Plugin.Log.LogWarning($"[联机] 服务器处理 {c.Name} 的消息 {(Msg)body[0]} 出错：{e.Message}"); }
                        }
                        if (clients[i] != c) continue;
                        if (c.Conn.Closed) Drop(c, c.Conn.CloseReason);
                        else if (c.Conn.Silence > TimeoutSeconds) Drop(c, "连接超时");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"[联机] 服务器出错：{e}");
                }
                Thread.Sleep(1);
            }
            // Net.ServerAutoShutdown: the host left, everyone is told.
            var bye = Kick(NetText.Key("Net.ServerAutoShutdown"));
            foreach (var c in clients)
            {
                if (c == null) continue;
                c.Conn.Send(bye);
                c.Conn.CloseAfterSending("服务器已关闭");
            }
            try { listener.Stop(); }
            catch (Exception) { }
            Plugin.Log.LogInfo("[联机] 服务器已关闭");
        }

        void Accept(TcpClient tcp)
        {
            var conn = new Connection(tcp);
            int slot = -1, active = 0;
            for (int i = 0; i < clients.Length; i++)
            {
                if (clients[i] != null) active++;
                else if (slot < 0) slot = i;
            }
            if (slot < 0 || active >= maxPlayers)
            {
                // CLI.ServerIsFull
                conn.Send(Kick(NetText.Key("CLI.ServerIsFull")));
                conn.CloseAfterSending("full");
                return;
            }
            clients[slot] = new Client { Conn = conn, Slot = slot };
            Plugin.Log.LogInfo($"[联机] {conn.Remote} 正在连接……（槽位 {slot}）");
        }

        static byte[] Kick(NetText reason)
        {
            var p = new Packet(Msg.Kick);
            reason.Write(p.W);
            return p.Done();
        }

        void KickClient(Client c, NetText reason)
        {
            Plugin.Log.LogInfo($"[联机] 踢出 {c.Conn.Remote}（{c.Name}）");
            c.Conn.Send(Kick(reason));
            c.Conn.CloseAfterSending("踢出");
            Drop(c, "踢出", false);
        }

        void Drop(Client c, string why, bool close = true)
        {
            if (clients[c.Slot] != c) return;
            clients[c.Slot] = null;
            if (close) c.Conn.Close(why);
            if (c.State != 2) return;
            PlayerCount = Math.Max(0, PlayerCount - 1);
            Plugin.Log.LogInfo($"[联机] {c.Name} 离开了（{why}）");
            var p = new Packet(Msg.PlayerActive);
            p.W.Write((byte)c.Slot);
            p.W.Write(false);
            Broadcast(p.Done(), null);
            // LegacyMultiplayer.20: "{0} has left."
            Chat(NetText.Key("LegacyMultiplayer.20", c.Name), Yellow, null);
            RecomputeOwners();
        }

        static readonly Color32 Yellow = new Color32(255, 240, 20, 255);
        static readonly Color32 DeathRed = new Color32(225, 25, 25, 255);
        static readonly Color32 EmoteOrange = new Color32(200, 100, 0, 255);

        /// <summary>Main.teamColor: none, red, green, blue, yellow, pink.</summary>
        public static readonly Color32[] TeamColors =
        {
            new Color32(255, 255, 255, 255), new Color32(218, 59, 59, 255), new Color32(59, 218, 85, 255),
            new Color32(59, 149, 218, 255), new Color32(242, 221, 100, 255), new Color32(224, 100, 242, 255),
        };

        void Broadcast(byte[] packet, Client except)
        {
            foreach (var c in clients)
                if (c != null && c != except && c.State == 2) c.Conn.Send(packet);
        }

        /// <summary>A received message passed on to the other players, its first field (the sender's slot) made the sender's.</summary>
        void Relay(Client from, byte[] body, Func<Client, bool> to = null)
        {
            if (body.Length > 1) body[1] = (byte)from.Slot;
            var framed = Packet.Frame(body);
            foreach (var c in clients)
                if (c != null && c != from && c.State == 2 && (to == null || to(c))) c.Conn.Send(framed);
        }

        /// <summary>A line in everyone's chat (or one player's): author 255 is the server.</summary>
        void Chat(NetText line, Color32 color, Client only, int author = 255)
        {
            var p = new Packet(Msg.Chat);
            p.W.Write((byte)author);
            line.Write(p.W);
            p.W.Write(color.r);
            p.W.Write(color.g);
            p.W.Write(color.b);
            var data = p.Done();
            if (only != null) only.Conn.Send(data);
            else Broadcast(data, null);
        }

        void Handle(Client c, byte[] body)
        {
            var type = (Msg)body[0];
            using (var r = Packet.Read(body))
            {
                switch (type)
                {
                    case Msg.Hello:
                    {
                        if (c.State != 0) return;
                        string version = r.ReadString();
                        string key = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
                        c.IsHost = key == HostKey;
                        if (version != Protocol)
                        {
                            // LegacyMultiplayer.4: "You are not using the same version as this server."
                            KickClient(c, NetText.Mod("{0}（服务器：{1}，你：{2}）", NetText.Key("LegacyMultiplayer.4"), Protocol, version));
                            return;
                        }
                        if (password.Length == 0 || c.IsHost) GiveSlot(c);
                        else
                        {
                            c.State = -1;
                            c.Conn.Send(new Packet(Msg.RequestPassword).Done());
                        }
                        return;
                    }
                    case Msg.SendPassword:
                        if (c.State != -1) return;
                        if (r.ReadString() == password) GiveSlot(c);
                        else KickClient(c, NetText.Key("LegacyMultiplayer.1"));
                        return;
                    case Msg.PlayerInfo:
                        PlayerInfo(c, body, r);
                        return;
                    case Msg.Ping:
                        c.Conn.Send(Packet.Frame(body));
                        return;
                }
                if (c.State != 2) return;
                switch (type)
                {
                    case Msg.WorldRequest:
                    case Msg.WorldAction:
                        body[1] = (byte)c.Slot;
                        foreach (var host in clients)
                            if (host != null && host.State == 2 && host.IsHost) host.Conn.Send(Packet.Frame(body));
                        return;
                    case Msg.WorldState:
                    case Msg.WorldResult:
                        if (!c.IsHost) return;
                        body[1] = (byte)c.Slot;
                        int target = body[2];
                        foreach (var peer in clients)
                            if (peer != null && peer.State == 2 && peer != c && (target == 255 || target == peer.Slot))
                                peer.Conn.Send(Packet.Frame(body));
                        return;
                    case Msg.PlayerState:
                        body[1] = (byte)c.Slot;
                        c.LastState = body;
                        Relay(c, body);
                        return;
                    case Msg.PlayerScenes:
                    {
                        r.ReadByte();
                        var inNow = new List<string>();
                        int n = r.ReadByte();
                        for (int i = 0; i < n; i++) inNow.Add(r.ReadString());
                        c.Loaded.Clear();
                        n = r.ReadByte();
                        for (int i = 0; i < n; i++) c.Loaded.Add(r.ReadString());
                        foreach (var s in inNow)
                        {
                            c.Loaded.Add(s);
                            if (!c.In.ContainsKey(s)) c.In[s] = tick;
                        }
                        var left = new List<string>();
                        foreach (var s in c.In.Keys) if (!inNow.Contains(s)) left.Add(s);
                        foreach (var s in left) c.In.Remove(s);
                        body[1] = (byte)c.Slot;
                        c.LastLook = body;
                        Relay(c, body);
                        RecomputeOwners();
                        return;
                    }
                    case Msg.Chat:
                        ChatFrom(c, r.ReadString());
                        return;
                    case Msg.TogglePvp:
                    {
                        r.ReadByte();
                        c.Pvp = r.ReadBoolean();
                        Relay(c, body);
                        // LegacyMultiplayer.11 / 12: "{0} has enabled / disabled PvP!"
                        Chat(NetText.Key(c.Pvp ? "LegacyMultiplayer.11" : "LegacyMultiplayer.12", c.Name), TeamColors[c.Team], null);
                        return;
                    }
                    case Msg.TeamChange:
                    {
                        r.ReadByte();
                        c.Team = (byte)Mathf.Clamp(r.ReadByte(), 0, 5);
                        Relay(c, body);
                        // LegacyMultiplayer.13-17, 22: no longer on a team; joined the red, green, blue, yellow, pink team.
                        string key = c.Team == 0 ? "LegacyMultiplayer.13" : c.Team == 5 ? "LegacyMultiplayer.22" : "LegacyMultiplayer." + (13 + c.Team);
                        Chat(NetText.Key(key, c.Name), TeamColors[c.Team], null);
                        return;
                    }
                    case Msg.EnemySnapshot:
                    {
                        r.ReadByte();
                        string scene = r.ReadString();
                        Relay(c, body, o => o.Loaded.Contains(scene));
                        return;
                    }
                    case Msg.DamageNpc:
                    {
                        r.ReadByte();
                        string scene = r.ReadString();
                        if (!owners.TryGetValue(scene, out int owner) || owner == c.Slot) return;
                        var o = clients[owner];
                        if (o != null && o.State == 2)
                        {
                            body[1] = (byte)c.Slot;
                            o.Conn.Send(Packet.Frame(body));
                        }
                        return;
                    }
                    case Msg.PlayerDeath:
                    {
                        r.ReadByte();
                        bool pvp = r.ReadBoolean();
                        var line = NetText.Read(r);
                        c.Deaths++;
                        if (pvp) c.PvpDeaths++;
                        Relay(c, body);
                        Chat(line, DeathRed, null);
                        return;
                    }
                    case Msg.SetTime:
                        if (c.IsHost) Relay(c, body);
                        return;
                    case Msg.BossSummon:
                    {
                        r.ReadByte();
                        r.ReadString();
                        body[1] = (byte)c.Slot;
                        Relay(c, body);
                        Chat(NetText.Mod("{0} 使用了首领召唤物，所有人都被传送到首领的竞技场", c.Name), Yellow, null);
                        return;
                    }
                    case Msg.ProjectileSnapshot:
                    case Msg.MinionSnapshot:
                    case Msg.TileChange:
                    case Msg.PlayerHurt:
                    case Msg.EnemyHit:
                    case Msg.EnemyDeath:
                        Relay(c, body);
                        return;
                }
            }
        }

        void GiveSlot(Client c)
        {
            c.State = 1;
            var p = new Packet(Msg.PlayerSlot);
            p.W.Write((byte)c.Slot);
            c.Conn.Send(p.Done());
        }

        /// <summary>
        /// MessageBuffer case 4 on the server: the name checked (not empty, not too long, nobody else's
        /// already), then the first time the player is in: they get everyone already there, everyone
        /// gets them, and "{0} has joined." goes to all.
        /// </summary>
        void PlayerInfo(Client c, byte[] body, BinaryReader r)
        {
            if (c.State < 1) return;
            r.ReadByte();
            string name = (r.ReadString() ?? "").Trim();
            if (name.Length == 0)
            {
                KickClient(c, NetText.Key("Net.EmptyName"));
                return;
            }
            if (name.Length > 20)
            {
                KickClient(c, NetText.Key("Net.NameTooLong"));
                return;
            }
            foreach (var o in clients)
                if (o != null && o != c && o.State == 2 && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    // LegacyMultiplayer.5: "{0} is already on this server."
                    KickClient(c, NetText.Key("LegacyMultiplayer.5", name));
                    return;
                }
            c.Name = name;
            body[1] = (byte)c.Slot;
            c.Info = body;
            if (c.State == 2)
            {
                Relay(c, body);
                return;
            }
            c.State = 2;
            PlayerCount++;
            // Everyone already here, as they are now.
            foreach (var o in clients)
            {
                if (o == null || o == c || o.State != 2) continue;
                c.Conn.Send(Packet.Frame(o.Info));
                c.Conn.Send(PvpPacket(o));
                c.Conn.Send(TeamPacket(o));
                if (o.LastLook != null) c.Conn.Send(Packet.Frame(o.LastLook));
                if (o.LastState != null) c.Conn.Send(Packet.Frame(o.LastState));
            }
            foreach (var kv in owners) c.Conn.Send(OwnerPacket(kv.Key, kv.Value));
            Relay(c, body);
            var done = new Packet(Msg.FinishedConnecting);
            done.W.Write((byte)c.Slot);
            c.Conn.Send(done.Done());
            Plugin.Log.LogInfo($"[联机] {name} 已加入（{c.Conn.Remote}，槽位 {c.Slot}{(c.IsHost ? "，主机" : "")}）");
            // LegacyMultiplayer.19: "{0} has joined."
            Chat(NetText.Key("LegacyMultiplayer.19", name), Yellow, null);
        }

        static byte[] PvpPacket(Client o)
        {
            var p = new Packet(Msg.TogglePvp);
            p.W.Write((byte)o.Slot);
            p.W.Write(o.Pvp);
            return p.Done();
        }

        static byte[] TeamPacket(Client o)
        {
            var p = new Packet(Msg.TeamChange);
            p.W.Write((byte)o.Slot);
            p.W.Write(o.Team);
            return p.Done();
        }

        static byte[] OwnerPacket(string scene, int owner)
        {
            var p = new Packet(Msg.SceneOwner);
            p.W.Write(scene);
            p.W.Write((byte)owner);
            return p.Done();
        }

        /// <summary>
        /// Who runs each scene's enemies: whoever already does, as long as they still have the scene
        /// loaded and either stand in it or nobody does; else the one who has stood in it longest; else
        /// anyone who has it loaded (a neighbour scene beside them). A change goes to everyone.
        /// </summary>
        void RecomputeOwners()
        {
            var scenes = new HashSet<string>();
            foreach (var c in clients)
                if (c != null && c.State == 2)
                    foreach (var s in c.Loaded) scenes.Add(s);
            var gone = new List<string>();
            foreach (var kv in owners)
                if (!scenes.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var s in gone)
            {
                owners.Remove(s);
                Broadcast(OwnerPacket(s, 255), null);
            }
            foreach (var s in scenes)
            {
                owners.TryGetValue(s, out int now);
                if (!owners.ContainsKey(s)) now = -1;
                var cur = now >= 0 ? clients[now] : null;
                bool curOk = cur != null && cur.State == 2 && cur.Loaded.Contains(s);
                Client inIt = null;
                foreach (var c in clients)
                    if (c != null && c.State == 2 && c.In.TryGetValue(s, out long since) && (inIt == null || since < inIt.In[s])) inIt = c;
                Client pick;
                if (curOk && (inIt == null || cur.In.ContainsKey(s))) pick = cur;
                else if (inIt != null) pick = inIt;
                else if (curOk) pick = cur;
                else
                {
                    pick = null;
                    foreach (var c in clients)
                        if (c != null && c.State == 2 && c.Loaded.Contains(s)) { pick = c; break; }
                }
                if (pick == null || pick.Slot == now) continue;
                owners[s] = pick.Slot;
                Broadcast(OwnerPacket(s, pick.Slot), null);
            }
        }

        // ------------------------------------------------------------------ chat (ChatCommandProcessor)

        static readonly System.Random dice = new System.Random();

        /// <summary>A command (ChatCommand.*: the same words in every language Terraria has) and what follows it.</summary>
        static bool Is(string text, out string rest, params string[] names)
        {
            rest = null;
            foreach (var name in names)
            {
                if (text.Equals(name, StringComparison.OrdinalIgnoreCase)) { rest = ""; return true; }
                if (text.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)) { rest = text.Substring(name.Length + 1).Trim(); return true; }
            }
            return false;
        }

        void ChatFrom(Client c, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.Length > 500) text = text.Substring(0, 500);
            if (text.StartsWith("/"))
            {
                if (Is(text, out var msg, "/p"))
                {
                    if (c.Team == 0)
                    {
                        Chat(NetText.Key("LegacyMultiplayer.10"), Yellow, c);
                        return;
                    }
                    var p = ChatPacket(c.Slot, NetText.Literal(msg), TeamColors[c.Team]);
                    foreach (var o in clients)
                        if (o != null && o.State == 2 && o.Team == c.Team) o.Conn.Send(p);
                    return;
                }
                if (Is(text, out msg, "/me"))
                {
                    Chat(NetText.Literal("*" + c.Name + " " + msg), EmoteOrange, null);
                    return;
                }
                if (Is(text, out _, "/roll"))
                {
                    int n;
                    lock (dice) n = dice.Next(1, 101);
                    // LegacyMultiplayer.9: "rolls a"
                    Chat(NetText.Mod("*{0} {1} {2}", c.Name, NetText.Key("LegacyMultiplayer.9"), n), Yellow, null);
                    return;
                }
                if (Is(text, out _, "/playing", "/players"))
                {
                    var names = new List<string>();
                    foreach (var o in clients) if (o != null && o.State == 2) names.Add(o.Name);
                    // LegacyMultiplayer.7: "Current players:"
                    Chat(NetText.Mod("{0} {1}", NetText.Key("LegacyMultiplayer.7"), string.Join(", ", names)), Yellow, c);
                    return;
                }
                if (Is(text, out _, "/death"))
                {
                    Chat(NetText.Key("LegacyMultiplayer.23", c.Name, c.Deaths), Yellow, null);
                    return;
                }
                if (Is(text, out _, "/pvpdeath"))
                {
                    Chat(NetText.Key("LegacyMultiplayer.24", c.Name, c.PvpDeaths), Yellow, null);
                    return;
                }
                if (Is(text, out _, "/alldeath"))
                {
                    foreach (var o in clients) if (o != null && o.State == 2) Chat(NetText.Key("LegacyMultiplayer.23", o.Name, o.Deaths), Yellow, null);
                    return;
                }
                if (Is(text, out _, "/allpvpdeath"))
                {
                    foreach (var o in clients) if (o != null && o.State == 2) Chat(NetText.Key("LegacyMultiplayer.24", o.Name, o.PvpDeaths), Yellow, null);
                    return;
                }
                if (Is(text, out _, "/help"))
                {
                    foreach (var k in new[] { "Party", "Emote", "Playing", "Roll", "Death", "PVPDeath", "AllDeath", "AllPVPDeath", "Help" })
                        Chat(NetText.Key("ChatCommandDescription." + k), Yellow, c);
                    return;
                }
            }
            Broadcast(ChatPacket(c.Slot, NetText.Literal(text), new Color32(255, 255, 255, 255)), null);
        }

        static byte[] ChatPacket(int author, NetText line, Color32 color)
        {
            var p = new Packet(Msg.Chat);
            p.W.Write((byte)author);
            line.Write(p.W);
            p.W.Write(color.r);
            p.W.Write(color.g);
            p.W.Write(color.b);
            return p.Done();
        }

        /// <summary>This computer's LAN addresses, for the host to tell the others.</summary>
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (var a in ni.GetIPProperties().UnicastAddresses)
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork && !list.Contains(a.Address.ToString())) list.Add(a.Address.ToString());
                }
            }
            catch (Exception)
            {
                try
                {
                    foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                        if (a.AddressFamily == AddressFamily.InterNetwork && !list.Contains(a.ToString())) list.Add(a.ToString());
                }
                catch (Exception) { }
            }
            // Private ranges first (what a LAN friend types).
            list.Sort((a, b) => Private(b).CompareTo(Private(a)));
            return list;
        }

        static bool Private(string ip) => ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172.");
    }
}
