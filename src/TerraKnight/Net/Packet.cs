using System;
using System.IO;
using System.Text;
using TerraKnight.Terraria;

namespace TerraKnight.Net
{
    /// <summary>
    /// The messages. Those that do the job of one of Terraria's keep its number (MessageID: Hello 1,
    /// Kick 2, PlayerInfo 3/4, PlayerControls 13, PlayerActive 14, TileManipulation 17, SetTime 18,
    /// SyncNPC 23, SyncProjectile 27, DamageNPC 28, TogglePVP 30, SyncPlayerZone 36, RequestPassword
    /// 37, SendPassword 38, TeamChange 45, NetModules 82 (the chat), PlayerHurtV2 117, PlayerDeathV2
    /// 118, FinishedConnectingToServer 129, SetCountsAsHostForGameplay 139, Ping 154); the mod's own
    /// from 200.
    /// </summary>
    internal enum Msg : byte
    {
        Hello = 1,
        Kick = 2,
        PlayerSlot = 3,
        PlayerInfo = 4,
        PlayerState = 13,
        PlayerActive = 14,
        TileChange = 17,
        SetTime = 18,
        EnemySnapshot = 23,
        ProjectileSnapshot = 27,
        DamageNpc = 28,
        TogglePvp = 30,
        PlayerScenes = 36,
        RequestPassword = 37,
        SendPassword = 38,
        TeamChange = 45,
        Chat = 82,
        PlayerHurt = 117,
        PlayerDeath = 118,
        FinishedConnecting = 129,
        SceneOwner = 139,
        Ping = 154,
        /// <summary>An enemy of an owned scene died (its owner tells the others).</summary>
        EnemyDeath = 200,
        /// <summary>An enemy of an owned scene took a hit (the owner tells the others: its number and flash).</summary>
        EnemyHit = 201,
        /// <summary>A minion snapshot (the owner's minions, drawn by the others).</summary>
        MinionSnapshot = 202,
    }

    /// <summary>
    /// One message on the wire as Terraria frames it (MessageBuffer): its whole length in two bytes,
    /// its type, then its fields, little-endian, strings with a 7-bit length and UTF-8 (BinaryWriter).
    /// </summary>
    internal sealed class Packet
    {
        public const int MaxSize = ushort.MaxValue;
        readonly MemoryStream ms = new MemoryStream(64);
        public readonly BinaryWriter W;

        public Packet(Msg type)
        {
            W = new BinaryWriter(ms, Encoding.UTF8);
            W.Write((ushort)0);
            W.Write((byte)type);
        }

        /// <summary>The finished message, its length filled in.</summary>
        public byte[] Done()
        {
            W.Flush();
            var data = ms.ToArray();
            if (data.Length > MaxSize) throw new InvalidOperationException($"消息 {(Msg)data[2]} 太长：{data.Length} 字节");
            data[0] = (byte)data.Length;
            data[1] = (byte)(data.Length >> 8);
            return data;
        }

        /// <summary>How long it is so far (to keep a list under the size limit).</summary>
        public long Length => ms.Length;

        /// <summary>A received message (its type and fields) framed again to pass on.</summary>
        public static byte[] Frame(byte[] body)
        {
            var data = new byte[body.Length + 2];
            data[0] = (byte)data.Length;
            data[1] = (byte)(data.Length >> 8);
            Buffer.BlockCopy(body, 0, data, 2, body.Length);
            return data;
        }

        /// <summary>A reader over a received message's fields (after its type).</summary>
        public static BinaryReader Read(byte[] body) => new BinaryReader(new MemoryStream(body, 1, body.Length - 1, false), Encoding.UTF8);
    }

    /// <summary>
    /// Terraria's NetworkText: a line sent as what it is made of, so each player reads it in their
    /// own language. A literal; one of Terraria's keys (its text from Terraria.exe in the reader's
    /// language) with its arguments; or a line of the mod's own (its Simplified Chinese, looked up in
    /// the mod's table) with its arguments.
    /// </summary>
    internal sealed class NetText
    {
        enum Mode : byte { Literal, Terraria, Mod }

        Mode mode;
        string text;
        NetText[] args = Array.Empty<NetText>();

        public static NetText Literal(string s) => new NetText { mode = Mode.Literal, text = s ?? "" };

        /// <summary>One of Terraria's lines by key ("LegacyMultiplayer.19"); arguments are NetTexts or anything else as a literal.</summary>
        public static NetText Key(string key, params object[] args) => new NetText { mode = Mode.Terraria, text = key, args = Wrap(args) };

        /// <summary>A line of the mod's own, in its Simplified Chinese ("{0} 传送到了 {1}").</summary>
        public static NetText Mod(string zh, params object[] args) => new NetText { mode = Mode.Mod, text = zh, args = Wrap(args) };

        static NetText[] Wrap(object[] args)
        {
            var a = new NetText[args.Length];
            for (int i = 0; i < args.Length; i++) a[i] = args[i] as NetText ?? Literal(args[i]?.ToString());
            return a;
        }

        public void Write(BinaryWriter w)
        {
            w.Write((byte)mode);
            w.Write(text);
            if (mode == Mode.Literal) return;
            w.Write((byte)args.Length);
            foreach (var a in args) a.Write(w);
        }

        public static NetText Read(BinaryReader r, int depth = 0)
        {
            var t = new NetText { mode = (Mode)r.ReadByte(), text = r.ReadString() };
            if (t.mode == Mode.Literal) return t;
            int n = r.ReadByte();
            if (depth > 4) throw new InvalidDataException("NetText 嵌套太深");
            t.args = new NetText[n];
            for (int i = 0; i < n; i++) t.args[i] = Read(r, depth + 1);
            return t;
        }

        /// <summary>In the current language.</summary>
        public override string ToString()
        {
            switch (mode)
            {
                case Mode.Literal: return text;
                case Mode.Terraria: return Fill(TerrariaText.Get(Lang.Culture, text) ?? TerrariaText.Get("en-US", text) ?? text);
                default: return Fill(Lang.T(text));
            }
        }

        /// <summary>{0}, {1}... filled without string.Format (Terraria's lines have other braces in them).</summary>
        string Fill(string pattern)
        {
            if (args.Length == 0 || pattern == null) return pattern;
            var sb = new StringBuilder(pattern);
            for (int i = 0; i < args.Length; i++) sb.Replace("{" + i + "}", args[i].ToString());
            return sb.ToString();
        }
    }
}
