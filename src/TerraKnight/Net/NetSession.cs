using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using GlobalEnums;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// This game's side of a LAN game (Netplay's client loop, MessageBuffer's client cases):
    /// joining a server by its address (Hello, the password if it asks, the slot, the player's
    /// data), then every frame what came in and what goes out: the player's state twenty times a
    /// second, the scenes they are in, their projectiles, the enemies of the scenes they run, the
    /// blocks they change. Hosting starts a <see cref="Server"/> in this game and joins it. The
    /// session ends when the player leaves the game for the title menu (the host's ends the server:
    /// Net.ServerAutoShutdown), or when the connection goes.
    /// </summary>
    internal static class NetSession
    {
        public enum Phase { Off, Connecting, Handshake, NeedPassword, Joining, Ready }

        public static Phase State { get; private set; }
        public static bool Active => State == Phase.Ready && conn != null && !conn.Closed;
        public static bool Hosting => server != null;
        public static Server Server => server;
        public static int MySlot { get; private set; } = -1;
        public static string Address { get; private set; }
        public static int Port { get; private set; }
        /// <summary>What the menu shows while joining.</summary>
        public static NetText Status { get; private set; }
        /// <summary>Why the last session ended (null while none has).</summary>
        public static NetText Ended { get; private set; }
        public static bool Pvp { get; private set; }
        public static byte Team { get; private set; }
        public static float PingMs { get; private set; }
        public static string MyName { get; private set; }

        public static readonly Dictionary<int, RemotePlayer> Players = new Dictionary<int, RemotePlayer>();
        /// <summary>Which player runs each scene's enemies (the server's SceneOwner).</summary>
        public static readonly Dictionary<string, int> Owners = new Dictionary<string, int>();

        static Server server;
        static Connection conn;
        static TcpClient tcp;
        static Task connecting;
        static float connectStart;
        static string hostKey;
        static float nextState, nextScenes, nextPing, pingSentAt, nextTime;
        static readonly List<string> inScenes = new List<string>(), loaded = new List<string>();
        static string scenesKey = "";
        static float scenesSentAt;
        static bool wasInGame;

        /// <summary>Dev: messages handled so far by type.</summary>
        public static readonly Dictionary<Msg, int> DevReceived = new Dictionary<Msg, int>();

        /// <summary>"Host &amp; Play": the server on this computer, then this game joins it.</summary>
        public static bool Host(int port, string password, int maxPlayers, out string error)
        {
            Leave(null);
            server = Server.Start(port, password, maxPlayers, out error);
            if (server == null) return false;
            hostKey = server.HostKey;
            Connect("127.0.0.1", port);
            // LegacyMenu.8: "Starting server..."
            Status = NetText.Key("LegacyMenu.8");
            return true;
        }

        /// <summary>"Join via IP".</summary>
        public static void Join(string address, int port)
        {
            Leave(null);
            hostKey = null;
            Connect(address, port);
        }

        /// <summary>The game's own setting, while a session keeps it running in the background.</summary>
        static bool? runInBackgroundBefore;

        static void Connect(string address, int port)
        {
            // A LAN game goes on with the window in the background (Terraria's does): the others' enemies and the server don't wait for it.
            if (runInBackgroundBefore == null)
            {
                runInBackgroundBefore = Application.runInBackground;
                Application.runInBackground = true;
            }
            Address = address;
            Port = port;
            Ended = null;
            MyName = NetSettings.PlayerName;
            tcp = new TcpClient { NoDelay = true };
            try
            {
                connecting = tcp.ConnectAsync(address, port);
            }
            catch (Exception e)
            {
                Fail(NetText.Mod("无法连接到 {0}：{1}", address, e.Message));
                return;
            }
            connectStart = Time.unscaledTime;
            State = Phase.Connecting;
            // Net.ConnectingTo: "Connecting to {0}"
            Status = NetText.Key("Net.ConnectingTo", address + ":" + port);
            Plugin.Log.LogInfo($"[联机] 连接 {address}:{port}，名字 {MyName}");
        }

        public static void SubmitPassword(string password)
        {
            if (State != Phase.NeedPassword) return;
            var p = new Packet(Msg.SendPassword);
            p.W.Write(password ?? "");
            Send(p.Done());
            State = Phase.Handshake;
            Status = NetText.Key("Net.SendingPlayerData");
        }

        /// <summary>Ends the session (and the server, when hosting); why, if it ended on its own, for the menu.</summary>
        public static void Leave(NetText why)
        {
            bool was = State != Phase.Off;
            if (Active) World.Chests.Close(false);
            if (conn != null) conn.CloseAfterSending("离开");
            conn = null;
            try { tcp?.Close(); }
            catch (Exception) { }
            tcp = null;
            connecting = null;
            if (server != null)
            {
                server.Stop();
                server = null;
            }
            foreach (var p in Players.Values) p.Destroy();
            Players.Clear();
            Owners.Clear();
            Enemies.Reset();
            Items.TerraProjectiles.Instance?.NetClearGhosts();
            Items.Minions.Instance?.NetClearGhosts();
            TileSync.Reset();
            WorldSync.Reset();
            World.TerraTime.NetOffset = 0f;
            State = Phase.Off;
            if (runInBackgroundBefore != null)
            {
                Application.runInBackground = runInBackgroundBefore.Value;
                runInBackgroundBefore = null;
            }
            MySlot = -1;
            Pvp = false;
            Team = 0;
            Status = null;
            scenesKey = "";
            wasInGame = false;
            if (why != null) Ended = why;
            if (was) Plugin.Log.LogInfo($"[联机] 会话结束{(why != null ? "：" + why : "")}");
        }

        static void Fail(NetText why)
        {
            Plugin.Log.LogWarning($"[联机] {why}");
            Leave(why);
            Ended = why;
        }

        public static void Send(byte[] packet)
        {
            if (conn != null && !conn.Closed) conn.Send(packet);
        }

        /// <summary>A message that starts with this player's slot (the server makes it theirs anyway).</summary>
        public static Packet Mine(Msg type)
        {
            var p = new Packet(type);
            p.W.Write((byte)Mathf.Max(0, MySlot));
            return p;
        }

        public static void SetPvp(bool on)
        {
            if (!Active || Pvp == on) return;
            Pvp = on;
            var p = Mine(Msg.TogglePvp);
            p.W.Write(on);
            Send(p.Done());
        }

        public static void SetTeam(byte team)
        {
            if (!Active || Team == team) return;
            Team = team;
            var p = Mine(Msg.TeamChange);
            p.W.Write(team);
            Send(p.Done());
        }

        /// <summary>A boss seal used here: the others are taken to its arena too (BossSummons.Follow).</summary>
        public static void SendBossSummon(string key)
        {
            if (!Active) return;
            var p = Mine(Msg.BossSummon);
            p.W.Write(key ?? "");
            Send(p.Done());
        }

        public static RemotePlayer Player(int slot) => Players.TryGetValue(slot, out var p) ? p : null;

        /// <summary>Every frame, first thing (TerraMod.Update).</summary>
        public static void Tick()
        {
            if (State == Phase.Off) return;
            if (State == Phase.Connecting)
            {
                if (!connecting.IsCompleted)
                {
                    // LegacyInterface.43: "Connection timed out"
                    if (Time.unscaledTime - connectStart > 10f) Fail(NetText.Key("LegacyInterface.43"));
                    return;
                }
                if (connecting.IsFaulted || tcp == null || !tcp.Connected)
                {
                    var e = connecting.Exception?.GetBaseException();
                    Fail(NetText.Mod("无法连接到 {0}：{1}", Address + ":" + Port, e != null ? e.Message : "?"));
                    return;
                }
                conn = new Connection(tcp);
                tcp = null;
                connecting = null;
                State = Phase.Handshake;
                // Net.FoundServer
                Status = NetText.Key("Net.FoundServer");
                var hello = new Packet(Msg.Hello);
                hello.W.Write(Server.Protocol);
                hello.W.Write(hostKey ?? "");
                Send(hello.Done());
            }
            for (int n = 0; n < 4000 && conn != null && conn.TryReceive(out var body); n++)
            {
                var type = (Msg)body[0];
                DevReceived[type] = DevReceived.TryGetValue(type, out int k) ? k + 1 : 1;
                try { Handle(body); }
                catch (Exception e) { Plugin.Log.LogWarning($"[联机] 处理消息 {type} 出错：{e}"); }
            }
            if (conn == null) return;
            if (conn.Closed)
            {
                Fail(NetText.Mod(conn.CloseReason ?? "连接已丢失"));
                return;
            }
            if (State != Phase.Ready) return;
            float now = Time.unscaledTime;
            if (now >= nextPing)
            {
                nextPing = now + 1f;
                pingSentAt = now;
                Send(Mine(Msg.Ping).Done());
            }
            // The session ends with the game: back at the title menu after playing in it.
            var gm = GameManager.UnsafeInstance;
            bool inGame = GameHooks.IsGameplay(gm) && TerraMod.Instance != null;
            if (inGame) wasInGame = true;
            else if (wasInGame && gm != null && gm.gameState == GameState.MAIN_MENU)
            {
                Leave(NetText.Mod("你离开了多人游戏"));
                return;
            }
            if (now >= nextScenes)
            {
                nextScenes = now + 0.25f;
                SendScenes(inGame);
            }
            if (now >= nextState)
            {
                // Twenty times a second.
                nextState = now + 0.05f;
                SendState(inGame);
                if (inGame)
                {
                    Items.TerraProjectiles.Instance?.NetSend();
                    Items.Minions.Instance?.NetSend();
                    Enemies.SendSnapshots();
                }
            }
            if (Hosting && inGame && now >= nextTime)
            {
                // SetTime (18): the host's world clock is everyone's.
                nextTime = now + 2f;
                var p = Mine(Msg.SetTime);
                p.W.Write(World.TerraTime.Now);
                Send(p.Done());
            }
            if (inGame)
            {
                WorldSync.Tick();
                TileSync.Flush();
                PvpHits.Tick();
                Enemies.Tick();
            }
        }

        static void SendScenes(bool inGame)
        {
            if (inGame) NetScenes.Collect(inScenes, loaded);
            else
            {
                inScenes.Clear();
                loaded.Clear();
            }
            string key = string.Join("|", inScenes) + "#" + string.Join("|", loaded);
            if (key == scenesKey && Time.unscaledTime - scenesSentAt < 5f) return;
            scenesKey = key;
            scenesSentAt = Time.unscaledTime;
            var p = Mine(Msg.PlayerScenes);
            p.W.Write((byte)inScenes.Count);
            foreach (var s in inScenes) p.W.Write(s);
            p.W.Write((byte)loaded.Count);
            foreach (var s in loaded) p.W.Write(s);
            Send(p.Done());
        }

        /// <summary>Whether this game has a scene loaded (it may run or follow its enemies).</summary>
        public static bool HaveScene(string scene) => inScenes.Contains(scene) || loaded.Contains(scene);

        static void SendState(bool inGame)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            var pose = mod.NetPose;
            if (!inGame) pose.Shown = false;
            var s = PlayerState.From(pose, inGame ? NetScenes.Current : "", mod);
            var p = Mine(Msg.PlayerState);
            s.Write(p.W);
            Send(p.Done());
        }

        static RemotePlayer GetOrAdd(int slot)
        {
            if (!Players.TryGetValue(slot, out var rp))
            {
                rp = new RemotePlayer { Slot = slot };
                Players[slot] = rp;
            }
            return rp;
        }

        static void Handle(byte[] body)
        {
            var type = (Msg)body[0];
            using (var r = Packet.Read(body))
            {
                switch (type)
                {
                    case Msg.Kick:
                        Fail(NetText.Read(r));
                        return;
                    case Msg.RequestPassword:
                        State = Phase.NeedPassword;
                        // LegacyMenu.3: "Server Requires Password:"
                        Status = NetText.Key("LegacyMenu.3");
                        return;
                    case Msg.PlayerSlot:
                    {
                        MySlot = r.ReadByte();
                        State = Phase.Joining;
                        Status = NetText.Key("Net.SendingPlayerData");
                        var p = Mine(Msg.PlayerInfo);
                        p.W.Write(MyName ?? "Knight");
                        p.W.Write((byte)Mathf.Clamp(NetSettings.Look.Value, 0, 255));
                        Send(p.Done());
                        return;
                    }
                    case Msg.FinishedConnecting:
                        State = Phase.Ready;
                        Status = null;
                        Plugin.Log.LogInfo($"[联机] 已加入 {Address}:{Port}，槽位 {MySlot}{(Hosting ? "（主机）" : "")}");
                        return;
                    case Msg.Ping:
                        PingMs = (Time.unscaledTime - pingSentAt) * 1000f;
                        return;
                }
                int slot = r.ReadByte();
                if (slot == MySlot && type != Msg.Chat && type != Msg.DamageNpc) return;
                switch (type)
                {
                    case Msg.PlayerInfo:
                    {
                        var rp = GetOrAdd(slot);
                        rp.Name = r.ReadString();
                        rp.Look = r.ReadByte();
                        return;
                    }
                    case Msg.PlayerActive:
                        if (!r.ReadBoolean() && Players.TryGetValue(slot, out var gone))
                        {
                            gone.Destroy();
                            Players.Remove(slot);
                            WorldSync.PlayerLeft(slot);
                            Items.TerraProjectiles.Instance?.NetDropGhosts(slot);
                            Items.Minions.Instance?.NetDropGhosts(slot);
                        }
                        return;
                    case Msg.PlayerState:
                        GetOrAdd(slot).Receive(PlayerState.Read(r));
                        return;
                    case Msg.PlayerScenes:
                    {
                        var rp = GetOrAdd(slot);
                        rp.In.Clear();
                        rp.Loaded.Clear();
                        int n = r.ReadByte();
                        for (int i = 0; i < n; i++) rp.In.Add(r.ReadString());
                        n = r.ReadByte();
                        for (int i = 0; i < n; i++) rp.Loaded.Add(r.ReadString());
                        return;
                    }
                    case Msg.TogglePvp:
                        GetOrAdd(slot).Pvp = r.ReadBoolean();
                        return;
                    case Msg.TeamChange:
                        GetOrAdd(slot).Team = r.ReadByte();
                        return;
                    case Msg.Chat:
                    {
                        // Server→client: the author (255 the server), the line, its colour.
                        var line = NetText.Read(r);
                        var c = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), 255);
                        NetChat.Receive(slot, line, c);
                        return;
                    }
                    case Msg.SceneOwner:
                    {
                        // Not slot-first: the scene, then its owner.
                        r.BaseStream.Position = 0;
                        string scene = r.ReadString();
                        int owner = r.ReadByte();
                        if (owner == 255) Owners.Remove(scene);
                        else Owners[scene] = owner;
                        Enemies.OwnerChanged(scene);
                        return;
                    }
                    case Msg.EnemySnapshot:
                        Enemies.ReceiveSnapshot(slot, r);
                        return;
                    case Msg.DamageNpc:
                        Enemies.ReceiveDamage(slot, r);
                        return;
                    case Msg.EnemyDeath:
                        Enemies.ReceiveDeath(slot, r);
                        return;
                    case Msg.EnemyHit:
                        Enemies.ReceiveHit(slot, r);
                        return;
                    case Msg.ProjectileSnapshot:
                        Items.TerraProjectiles.Instance?.NetReceive(slot, r);
                        return;
                    case Msg.MinionSnapshot:
                        Items.Minions.Instance?.NetReceive(slot, r);
                        return;
                    case Msg.TileChange:
                        if (Hosting || WorldSync.Ready) TileSync.Receive(r);
                        return;
                    case Msg.WorldRequest:
                        if (Hosting) WorldSync.RequestSnapshot(slot);
                        return;
                    case Msg.WorldState:
                        WorldSync.ReceiveState(r);
                        return;
                    case Msg.WorldAction:
                        if (Hosting) WorldSync.ReceiveAction(slot, r.ReadString());
                        return;
                    case Msg.WorldResult:
                        WorldSync.ReceiveResult(r);
                        return;
                    case Msg.PlayerHurt:
                        PvpHits.ReceiveHurt(slot, r);
                        return;
                    case Msg.PlayerDeath:
                        return;
                    case Msg.BossSummon:
                        Items.BossSummons.Follow(r.ReadString(), Players.TryGetValue(slot, out var summoner) ? summoner.Name : null);
                        return;
                    case Msg.SetTime:
                    {
                        float hostNow = r.ReadSingle();
                        World.TerraTime.NetOffset = 0f;
                        World.TerraTime.NetOffset = hostNow - World.TerraTime.Now;
                        return;
                    }
                }
            }
        }

        /// <summary>Each frame after the local player is posed: the others drawn where they are.</summary>
        public static void PosePlayers()
        {
            if (State == Phase.Off) return;
            foreach (var p in Players.Values) p.Pose();
        }

        public static void DrawGui()
        {
            if (State == Phase.Off || Event.current.type != EventType.Repaint) return;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            float ui = UI.GameView.Scale;
            foreach (var p in Players.Values) p.DrawGui(cam, ui);
        }
    }
}
