using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suite "net": the LAN game in one copy of the game. The test slot's game hosts a server and a
    /// bot (a second client in the same process, speaking the protocol by hand) joins it, claims
    /// King's Pass before the host gets there, and plays the other player: its character beside the
    /// hero, its chat, its PvP blows, its blocks and arrows, the enemies it runs (puppets here,
    /// hits sent to it, a death from it), then hands the scene to the host and hits the host's
    /// enemies through it. Also the Terraria settings (autoswing, a key rebound) and a fall after
    /// a wall slide.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int NetPort = 7791;
        const string NetPassword = "tk";
        DevNetBot bot;

        /// <summary>A second client in this process, written against the wire format.</summary>
        sealed class DevNetBot
        {
            public Connection Conn;
            public int Slot = -1;
            public bool Joined;
            public string Kicked;
            public readonly Dictionary<Msg, int> Got = new Dictionary<Msg, int>();
            public readonly List<string> Lines = new List<string>();
            public readonly List<(int Id, int Hp, Vector2 Pos)> HostEnemies = new List<(int, int, Vector2)>();
            public readonly List<(int Id, int Hk)> Damage = new List<(int, int)>();
            public int ProjCount, Hurts, Tiles;
            string password;
            string name;

            public int Count(Msg m) => Got.TryGetValue(m, out int n) ? n : 0;

            public IEnumerator Join(int port, string name, string password)
            {
                this.name = name;
                this.password = password;
                var tcp = new TcpClient { NoDelay = true };
                var task = tcp.ConnectAsync("127.0.0.1", port);
                float until = Time.realtimeSinceStartup + 5f;
                while (!task.IsCompleted && Time.realtimeSinceStartup < until) yield return null;
                if (!tcp.Connected)
                {
                    Kicked = "连不上";
                    yield break;
                }
                Conn = new Connection(tcp);
                var p = new Packet(Msg.Hello);
                p.W.Write(Server.Protocol);
                p.W.Write("");
                Conn.Send(p.Done());
                until = Time.realtimeSinceStartup + 10f;
                while (!Joined && Kicked == null && !Conn.Closed && Time.realtimeSinceStartup < until)
                {
                    Pump();
                    yield return null;
                }
                // A kick arrives just before the connection closes: what came in is read once more.
                yield return null;
                Pump();
            }

            public Packet Mine(Msg t)
            {
                var p = new Packet(t);
                p.W.Write((byte)Mathf.Max(0, Slot));
                return p;
            }

            public void Send(Packet p) => Conn?.Send(p.Done());

            public void Pump()
            {
                if (Conn == null) return;
                while (Conn.TryReceive(out var body))
                {
                    var t = (Msg)body[0];
                    Got[t] = Count(t) + 1;
                    using (var r = Packet.Read(body))
                    {
                        switch (t)
                        {
                            case Msg.Kick:
                                Kicked = NetText.Read(r).ToString();
                                break;
                            case Msg.RequestPassword:
                            {
                                var p = new Packet(Msg.SendPassword);
                                p.W.Write(password ?? "");
                                Conn.Send(p.Done());
                                break;
                            }
                            case Msg.PlayerSlot:
                            {
                                Slot = r.ReadByte();
                                var p = Mine(Msg.PlayerInfo);
                                p.W.Write(name);
                                p.W.Write((byte)3);
                                Send(p);
                                break;
                            }
                            case Msg.FinishedConnecting:
                                Joined = true;
                                break;
                            case Msg.Chat:
                                r.ReadByte();
                                Lines.Add(NetText.Read(r).ToString());
                                break;
                            case Msg.EnemySnapshot:
                            {
                                r.ReadByte();
                                r.ReadString();
                                int n = r.ReadUInt16();
                                HostEnemies.Clear();
                                for (int i = 0; i < n; i++)
                                {
                                    int id = r.ReadInt32();
                                    byte f = r.ReadByte();
                                    if ((f & 8) != 0) r.ReadString();
                                    var pos = new Vector2(r.ReadSingle(), r.ReadSingle());
                                    r.ReadSingle(); r.ReadSingle(); r.ReadSingle();
                                    r.ReadInt16(); r.ReadByte();
                                    int hp = r.ReadInt32();
                                    if ((f & 4) == 0 && (f & 1) != 0) HostEnemies.Add((id, hp, pos));
                                }
                                break;
                            }
                            case Msg.ProjectileSnapshot:
                                r.ReadByte();
                                r.ReadString();
                                ProjCount = Mathf.Max(ProjCount, r.ReadUInt16());
                                break;
                            case Msg.DamageNpc:
                                r.ReadByte();
                                r.ReadString();
                                Damage.Add((r.ReadInt32(), r.ReadInt32()));
                                break;
                            case Msg.PlayerHurt:
                                Hurts++;
                                break;
                            case Msg.TileChange:
                                r.ReadByte();
                                r.ReadString();
                                Tiles += r.ReadUInt16();
                                break;
                        }
                    }
                }
            }

            public void Scenes(string[] inScenes, string[] loaded)
            {
                var p = Mine(Msg.PlayerScenes);
                p.W.Write((byte)inScenes.Length);
                foreach (var s in inScenes) p.W.Write(s);
                p.W.Write((byte)loaded.Length);
                foreach (var s in loaded) p.W.Write(s);
                Send(p);
            }
        }

        IEnumerator PumpBot()
        {
            while (bot != null)
            {
                bot.Pump();
                yield return null;
            }
        }

        static void NetCheck(bool ok, string what) => Plugin.Log.LogInfo($"[AutoTest] 联机 {(ok ? "通过" : "失败")}：{what}{(ok ? "" : " [NET_FAIL]")}");

        /// <summary>Before the game: the menu's panels, the server up, the bot in (a wrong password kicked first), King's Pass claimed by it.</summary>
        IEnumerator NetBoot()
        {
            UIManager.instance.UIGoToProfileMenu();
            yield return new WaitForSeconds(2.5f);
            NetMenu.DevOpen();
            yield return new WaitForSeconds(0.4f);
            Shot("Menu_Title", "net_1_menu");
            yield return null;
            yield return null;
            NetMenu.DevShowHost();
            yield return new WaitForSeconds(0.3f);
            Shot("Menu_Title", "net_2_host");
            yield return null;
            yield return null;
            NetMenu.DevShowJoin();
            yield return new WaitForSeconds(0.3f);
            Shot("Menu_Title", "net_3_join");
            yield return null;
            yield return null;
            NetMenu.DevClose();
            yield return new WaitForSeconds(0.2f);

            bool hosted = NetSession.Host(NetPort, NetPassword, 8, out string error);
            NetCheck(hosted, $"开服 端口 {NetPort}{(error != null ? "：" + error : "")}");
            for (float t = 0f; t < 10f && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
            NetCheck(NetSession.Active, $"主机自己的客户端加入（槽位 {NetSession.MySlot}，名字 {NetSession.MyName}）");
            yield return new WaitForSeconds(0.5f);
            Shot("Menu_Title", "net_4_banner");
            yield return null;

            var wrong = new DevNetBot();
            yield return wrong.Join(NetPort, "Intruder", "nope");
            NetCheck(wrong.Kicked != null && !wrong.Joined, $"密码错误被踢：{wrong.Kicked}");
            var dup = new DevNetBot();
            yield return dup.Join(NetPort, NetSession.MyName, NetPassword);
            NetCheck(dup.Kicked != null && !dup.Joined, $"同名被踢：{dup.Kicked}");

            bot = new DevNetBot();
            yield return bot.Join(NetPort, "Bot", NetPassword);
            NetCheck(bot.Joined, $"机器人加入（槽位 {bot.Slot}），踢出原因 {bot.Kicked ?? "无"}");
            mod.StartCoroutine(PumpBot());
            // It is in King's Pass first: its game runs the enemies there.
            bot.Scenes(new[] { "Tutorial_01" }, new string[0]);
            yield return new WaitForSeconds(0.5f);
            NetCheck(NetSession.Owners.TryGetValue("Tutorial_01", out int owner) && owner == bot.Slot, $"国王山道的敌人归机器人（{owner}）");
        }

        IEnumerator NetTest(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = false;
            mod.Stats.FullHeal();
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = hero.transform.position;

            // --- The Terraria settings: the panel, autoswing, a key rebound and back. ---
            UI.IngameOptions.Show();
            yield return new WaitForSeconds(0.5f);
            Shot(scene, "net_5_options");
            yield return null;
            yield return null;
            var bow = ItemDb.Get(ItemType.WoodenBow);
            bool before = Equipment.AutoReuses(bow);
            UI.IngameOptions.DevToggleAutoReuse();
            bool after = Equipment.AutoReuses(bow);
            UI.IngameOptions.DevToggleAutoReuse();
            NetCheck(!before && after && !Equipment.AutoReuses(bow), $"自动开火：木弓 关 {before} → 开 {after} → 关 {Equipment.AutoReuses(bow)}");
            TerraKeys.Set(TerraKeys.Action.Jump, UnityEngine.InputSystem.Key.K);
            yield return null;
            yield return null;
            var acts = InputHandler.Instance.inputActions;
            bool hasK = acts.jump.HasBinding(new InControl.KeyBindingSource(InControl.Key.K));
            bool hasSpace = acts.jump.HasBinding(new InControl.KeyBindingSource(InControl.Key.Space)) && !TerraControls.DevGameOwns(acts.jump, InControl.Key.Space);
            TerraKeys.Listen(TerraKeys.Action.Throw);
            yield return new WaitForSeconds(0.2f);
            Shot(scene, "net_6_options_listen");
            yield return null;
            yield return null;
            TerraKeys.StopListening();
            TerraKeys.ResetAll();
            yield return null;
            yield return null;
            bool backSpace = acts.jump.HasBinding(new InControl.KeyBindingSource(InControl.Key.Space)) && !acts.jump.HasBinding(new InControl.KeyBindingSource(InControl.Key.K));
            NetCheck(hasK && !hasSpace && backSpace, $"改键：跳 改成 K 后 K={hasK} mod 加的空格还在={hasSpace}（游戏自己绑的空格 {TerraControls.DevGameOwns(acts.jump, InControl.Key.Space)}），恢复默认后只有空格={backSpace}");
            UI.IngameOptions.Hide();
            yield return new WaitForSeconds(0.3f);

            // --- A fall after a wall slide (WallslideMovement: every tick of it starts the fall again). ---
            yield return WallFall(hero, col, rb, home);

            // --- The other player beside the hero, swinging a sword, hurt. ---
            var me = NetScenes.ToLocal(scene, new Vector2(col.bounds.center.x, col.bounds.min.y));
            for (int i = 0; i < 50; i++)
            {
                var s = new PlayerState
                {
                    Scene = scene, Shown = true, Grounded = true, FacingRight = false, Feet = me + new Vector2(3f, 0f), Vel = Vector2.zero,
                    BoxCenter = me + new Vector2(3f, col.bounds.extents.y), BoxSize = col.bounds.size, Opacity = 1f, Swing = (i % 15) / 15f,
                    Item = ItemType.CopperBroadsword, ItemScale = 1f, Body = -1, Leg = -1, Life = 60, LifeMax = 100,
                    Eq = new TerraPlayerSprite.Equips { Head = 1, Body = 1, Legs = 1 },
                };
                var p = bot.Mine(Msg.PlayerState);
                s.Write(p.W);
                bot.Send(p);
                if (i == 40) Shot(scene, "net_7_other_player");
                yield return new WaitForSeconds(0.05f);
            }
            var rp = NetSession.Player(bot.Slot);
            NetCheck(rp != null && rp.Visible && rp.Name == "Bot", $"看得到机器人：{(rp != null ? $"{rp.Name} 显示={rp.Visible} 位置 {rp.Feet:F2}（主角 {hero.transform.position:F2}）" : "没有")}");
            // The inventory in a LAN game: the two settings buttons, PvP and the teams; the world map with the bot's head.
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.4f);
            Shot(scene, "net_7b_inventory");
            yield return null;
            yield return null;
            InventoryGui.Open = false;
            UI.WorldMap.Toggle();
            yield return new WaitForSeconds(0.6f);
            Shot(scene, "net_7c_map");
            yield return null;
            yield return null;
            if (UI.WorldMap.Open) UI.WorldMap.Toggle();
            yield return new WaitForSeconds(0.3f);

            // --- Chat, the commands. ---
            var p2 = new Packet(Msg.Chat);
            p2.W.Write("你好，骑士！");
            bot.Send(p2);
            NetChat.Say("/roll");
            NetChat.Say("/playing");
            NetChat.Say("一起去打假骑士");
            yield return new WaitForSeconds(0.5f);
            NetChat.Open();
            yield return new WaitForSeconds(0.2f);
            Shot(scene, "net_8_chat");
            yield return null;
            yield return null;
            string all = string.Join(" | ", bot.Lines);
            NetCheck(all.Contains("掷出了") && all.Contains("Bot") && all.Contains("一起去打假骑士"), $"聊天（机器人收到）：{all}");
            NetCheck(rp != null && rp.Overhead == "你好，骑士！", $"机器人的话在它头上：{rp?.Overhead}");

            // --- The enemies the bot runs: puppets here, following its snapshots. ---
            var hms = new List<HealthManager>();
            var s1 = NetScenes.FindScene(scene);
            foreach (var root in s1.GetRootGameObjects())
                foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                    if (root.name != "TK Enemy Templates") hms.Add(hm);
            var basePos = new Dictionary<HealthManager, Vector2>();
            foreach (var hm in hms) basePos[hm] = NetScenes.ToLocal(scene, hm.transform.position);
            var ids = new Dictionary<HealthManager, int>();
            for (int i = 0; i < hms.Count; i++) ids[hms[i]] = 1000 + i;
            HealthManager probe = null;
            float probeDist = float.MaxValue;
            foreach (var hm in hms)
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < probeDist)
                {
                    probe = hm;
                    probeDist = d;
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 联机：国王山道 {hms.Count} 个敌人，最近的 {(probe != null ? probe.name + " " + probeDist.ToString("F1") : "无")}");
            Vector2 lastSent = default;
            for (int k = 0; k < 40; k++)
            {
                var p = bot.Mine(Msg.EnemySnapshot);
                p.W.Write(scene);
                p.W.Write((ushort)hms.Count);
                foreach (var hm in hms)
                {
                    var at = basePos[hm] + new Vector2(0f, 1.5f + Mathf.Sin(k * 0.3f));
                    if (hm == probe) lastSent = at;
                    p.W.Write(ids[hm]);
                    p.W.Write((byte)(1 | 2 | 8));
                    p.W.Write(Net.Enemies.DevPathOf(hm));
                    p.W.Write(at.x);
                    p.W.Write(at.y);
                    p.W.Write(hm.transform.localScale.x);
                    p.W.Write(hm.transform.localScale.y);
                    p.W.Write(0f);
                    p.W.Write((short)-1);
                    p.W.Write((byte)0);
                    p.W.Write(hm.hp);
                }
                bot.Send(p);
                yield return new WaitForSeconds(1f / 15f);
            }
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "net_9_puppets");
            yield return null;
            NetCheck(Net.Enemies.DevPuppets > 0, $"傀儡：{Net.Enemies.DevReport()}");
            if (probe != null)
            {
                Vector2 now = probe.transform.position;
                NetScenes.ToWorld(scene, lastSent, out var want);
                NetCheck(Net.Enemies.IsPuppet(probe) && Vector2.Distance(now, want) < 0.6f && Net.Enemies.DevScriptsOff(probe),
                    $"{probe.name} 跟着机器人的位置：现在 {now:F2}，机器人发的 {want:F2}，脚本全关 {Net.Enemies.DevScriptsOff(probe)}");
                int hpBefore = probe.hp;
                var pcol = probe.GetComponent<Collider2D>() ?? probe.GetComponentInChildren<Collider2D>();
                if (pcol != null) mod.Melee.HitWith(pcol, 30, 3f, 0f, 1f, col.bounds.center);
                yield return new WaitForSeconds(0.4f);
                int id = ids[probe];
                bool sent = bot.Damage.Exists(d => d.Id == id);
                NetCheck(sent && probe.hp == hpBefore, $"打傀儡：伤害交给机器人 {sent}（{string.Join(",", bot.Damage)}），本地血量没变 {hpBefore}->{probe.hp}");
                // The bot's game killed it.
                var pd = bot.Mine(Msg.EnemyDeath);
                pd.W.Write(scene);
                pd.W.Write(id);
                pd.W.Write(0f);
                pd.W.Write((byte)AttackTypes.Generic);
                bot.Send(pd);
                yield return new WaitForSeconds(0.6f);
                NetCheck(probe == null || probe.isDead, $"机器人那边打死了它，这边也死了：{(probe != null ? probe.isDead.ToString() : "已销毁")}");
                Shot(scene, "net_10_puppet_dead");
                yield return null;
            }

            // --- The bot goes on (King's Pass still loaded for it): the host runs the enemies, the bot's hits come here. ---
            bot.Scenes(new string[0], new[] { "Tutorial_01" });
            yield return new WaitForSeconds(1.2f);
            NetCheck(NetSession.Owners.TryGetValue(scene, out int o2) && o2 == NetSession.MySlot && Net.Enemies.DevPuppets == 0, $"换主机：现在归 {o2}（自己 {NetSession.MySlot}），{Net.Enemies.DevReport()}");
            NetCheck(bot.Count(Msg.EnemySnapshot) > 0 && bot.HostEnemies.Count > 0, $"机器人收到主机的敌人快照 {bot.Count(Msg.EnemySnapshot)} 次，最后一次 {bot.HostEnemies.Count} 个活着的");
            if (bot.HostEnemies.Count > 0)
            {
                var target = bot.HostEnemies[0];
                HealthManager thm = null;
                foreach (var hm in hms) if (hm != null && Net.Enemies.DevIdOf(hm) == target.Id) thm = hm;
                int hp0 = thm != null ? thm.hp : -1;
                var pdmg = bot.Mine(Msg.DamageNpc);
                pdmg.W.Write(scene);
                pdmg.W.Write(target.Id);
                pdmg.W.Write(3);
                pdmg.W.Write(0f);
                pdmg.W.Write((byte)AttackTypes.Generic);
                pdmg.W.Write(0f);
                // The Terraria damage the owner turns into health (a crawler's few points take some 60).
                pdmg.W.Write(60);
                pdmg.W.Write((byte)0);
                pdmg.W.Write(0f);
                pdmg.W.Write(0f);
                bot.Send(pdmg);
                yield return new WaitForSeconds(0.4f);
                NetCheck(thm != null && (thm.hp < hp0 || thm.isDead), $"机器人打主机的敌人：{(thm != null ? thm.name : "?")} 血量 {hp0}->{(thm != null ? thm.hp : -1)}，主机结算 {Net.Enemies.DevApplied} 次");
            }

            // --- PvP: both on, the bot's blade over the hero. ---
            mod.Stats.FullHeal();
            yield return new WaitForSeconds(1.5f);
            NetSession.SetPvp(true);
            var pv = bot.Mine(Msg.TogglePvp);
            pv.W.Write(true);
            bot.Send(pv);
            yield return new WaitForSeconds(0.3f);
            int life0 = mod.Stats.Life;
            me = NetScenes.ToLocal(scene, new Vector2(col.bounds.center.x, col.bounds.min.y));
            for (int i = 0; i < 6; i++)
            {
                var s = new PlayerState
                {
                    Scene = scene, Shown = true, Grounded = true, FacingRight = false, Feet = me + new Vector2(1.2f, 0f), BoxCenter = me + new Vector2(1.2f, col.bounds.extents.y),
                    BoxSize = col.bounds.size, Opacity = 1f, Swing = 0.5f, Item = ItemType.CopperBroadsword, ItemScale = 1f, Body = -1, Leg = -1, Life = 60, LifeMax = 100,
                    Melee = true, MeleeBox = new Rect(NetScenes.ToLocal(scene, col.bounds.min) - Vector2.one * 0.2f, (Vector2)col.bounds.size + Vector2.one * 0.4f),
                    MeleeSerial = 7, MeleeDamage = 25, MeleeCrit = 0, MeleeKb = 5f, MeleeItem = ItemType.CopperBroadsword,
                };
                var p = bot.Mine(Msg.PlayerState);
                s.Write(p.W);
                bot.Send(p);
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(0.5f);
            NetCheck(mod.Stats.Life < life0 && bot.Hurts > 0, $"PvP：机器人的剑砍到主角 {life0}->{mod.Stats.Life}，它收到受伤消息 {bot.Hurts} 次，挨了 {PvpHits.DevHits} 下（一刀只算一次）");
            NetSession.SetPvp(false);
            mod.Stats.FullHeal();

            // --- Blocks: one placed by the bot appears here; one dug here reaches the bot. ---
            var region = TerraWorld.Live;
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.05f));
            var cell = new Vector2Int(feet.x - 3, feet.y + 2);
            for (int y = feet.y + 2; y < feet.y + 12 && TerraWorld.Get(cell.x, y) != CellKind.Empty; y++) cell.y = y + 1;
            int lx = cell.x - region.CellOffset.x, ly = cell.y - region.CellOffset.y;
            var was = TerraWorld.Get(cell.x, cell.y);
            var pt = bot.Mine(Msg.TileChange);
            pt.W.Write(region.SceneName);
            pt.W.Write((ushort)1);
            pt.W.Write(ly * region.Grid.Width + lx);
            pt.W.Write(WorldEdits.Pack(CellKind.Wood, false));
            pt.W.Write(TerraWorld.GetWall(cell.x, cell.y));
            bot.Send(pt);
            yield return new WaitForSeconds(0.4f);
            NetCheck(TerraWorld.Get(cell.x, cell.y) == CellKind.Wood, $"机器人放的木块出现在这边：{was} -> {TerraWorld.Get(cell.x, cell.y)}，收到 {TileSync.DevApplied} 格");
            int tiles0 = bot.Tiles;
            TerraWorld.Set(cell.x, cell.y, was);
            yield return new WaitForSeconds(0.4f);
            NetCheck(bot.Tiles > tiles0, $"这边改回去，机器人收到 {bot.Tiles - tiles0} 格");

            // --- Projectiles: the bot's arrow drawn here; the host's arrow reaches the bot. ---
            for (int k = 0; k < 10; k++)
            {
                var pp = bot.Mine(Msg.ProjectileSnapshot);
                pp.W.Write(scene);
                pp.W.Write((ushort)1);
                var at = NetScenes.ToLocal(scene, (Vector2)col.bounds.center + new Vector2(2.5f - k * 0.15f, 1.2f));
                pp.W.Write(77);
                pp.W.Write((ushort)ProjKind.WoodenArrow);
                pp.W.Write(at.x); pp.W.Write(at.y);
                pp.W.Write(-6f); pp.W.Write(0f);
                pp.W.Write(90f); pp.W.Write(0f); pp.W.Write(1f); pp.W.Write(0f);
                pp.W.Write(0f); pp.W.Write(0f); pp.W.Write(0f); pp.W.Write(0f); pp.W.Write(0f);
                pp.W.Write((ushort)0); pp.W.Write((sbyte)-1); pp.W.Write(10 + k); pp.W.Write(0);
                pp.W.Write((byte)0); pp.W.Write(5); pp.W.Write((byte)4);
                pp.W.Write(0f); pp.W.Write(0f); pp.W.Write(0f); pp.W.Write(0f);
                pp.W.Write((byte)0); pp.W.Write((ushort)ItemType.WoodenBow); pp.W.Write(0);
                bot.Send(pp);
                if (k == 6) Shot(scene, "net_11_ghost_arrow");
                yield return new WaitForSeconds(0.05f);
            }
            NetCheck(TerraProjectiles.Instance.DevGhosts == 1, $"机器人的箭画在这边：{TerraProjectiles.Instance.DevGhosts} 支");
            mod.DevUse(ItemType.WoodenBow, (Vector2)col.bounds.center + new Vector2(-6f, 2f));
            yield return new WaitForSeconds(0.4f);
            NetCheck(bot.ProjCount > 0, $"主角射的箭到了机器人那边：{bot.ProjCount} 支");
            NetCheck(bot.Count(Msg.SetTime) > 0, $"主机的时间发给了机器人 {bot.Count(Msg.SetTime)} 次");

            // --- The bot leaves. ---
            bot.Conn.Close("测试结束");
            yield return new WaitForSeconds(1.5f);
            NetCheck(NetSession.Player(bot.Slot) == null && NetChat.DevLastLine.Contains("Bot"), $"机器人离开：玩家表里没有它了，聊天最后一行「{NetChat.DevLastLine}」");
            bot = null;
            NetSession.Leave(null);
            yield return new WaitForSeconds(0.3f);
            NetCheck(NetSession.State == NetSession.Phase.Off && Net.Enemies.DevPuppets == 0, "结束会话");
        }

        /// <summary>
        /// A wall 30 cells high beside the hero: let go at its top the fall hurts (control); slid all the
        /// way down it (down held, the claws worn) the landing doesn't.
        /// </summary>
        IEnumerator WallFall(HeroController hero, BoxCollider2D col, Rigidbody2D rb, Vector3 home)
        {
            var wallTouchingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("touchingWallR");
            var wallSlidingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("wallSlidingR");
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.05f));
            int wallX = feet.x + 2;
            var wall = new List<Vector2Int>();
            for (int y = feet.y; y < feet.y + 34; y++)
            {
                if (TerraWorld.Get(wallX, y) == CellKind.Empty)
                {
                    TerraWorld.Set(wallX, y, CellKind.Stone);
                    wall.Add(new Vector2Int(wallX, y));
                }
                // The shaft beside it open all the way (no ledge to land on half way).
                for (int x = wallX - 3; x < wallX; x++)
                    if (TerraWorld.IsSolid(x, y)) TerraWorld.Set(x, y, CellKind.Empty);
            }
            float half = col.bounds.extents.x;
            var top = new Vector3(wallX * CellGrid.CellSize - half - 0.02f + (hero.transform.position.x - col.bounds.center.x), home.y + 30 * CellGrid.CellSize, home.z);
            foreach (bool slide in new[] { false, true })
            {
                mod.Stats.FullHeal();
                yield return new WaitForSeconds(0.2f);
                int life0 = mod.Stats.Life;
                hero.transform.position = top;
                rb.linearVelocity = Vector2.zero;
                hero.FaceRight();
                // Off the ground first (the frame of the move still has the old contact).
                for (float t = 0f; t < 1f && hero.cState.onGround; t += Time.deltaTime) yield return null;
                if (slide)
                {
                    TerraMovement.DevMove = 1;
                    TerraMovement.DevDown = true;
                    for (float t = 0f; t < 6f && !hero.cState.onGround; t += Time.deltaTime)
                    {
                        if (!hero.cState.wallSliding)
                        {
                            wallTouchingR(hero) = true;
                            wallSlidingR(hero) = true;
                            hero.cState.wallSliding = true;
                            hero.cState.touchingWall = true;
                        }
                        yield return null;
                    }
                    TerraMovement.DevMove = 0;
                    TerraMovement.DevDown = false;
                }
                float logAt = 0f;
                for (float t = 0f; t < 5f && !hero.cState.onGround; t += Time.deltaTime)
                {
                    if (t >= logAt)
                    {
                        logAt += 0.25f;
                        Plugin.Log.LogInfo($"[AutoTest] 滑墙{(slide ? "" : "对照")} {t:F2}s：位置 {hero.transform.position:F2} 速度 {rb.linearVelocity:F1} 着地 {hero.cState.onGround} 贴墙 {hero.cState.touchingWall} 滑墙 {hero.cState.wallSliding} 可操作 {GameHooks.PlayerHasControl()}");
                    }
                    yield return null;
                }
                yield return new WaitForSeconds(0.3f);
                int lost = life0 - mod.Stats.Life;
                Plugin.Log.LogInfo($"[AutoTest] 滑墙{(slide ? "" : "对照")}落地：位置 {hero.transform.position:F2}（起点 {top:F2}），血 {life0}->{mod.Stats.Life}，最后伤害 {TerraHurt.LastDamage}，" +
                                   $"免摔 {Equipment.Current.NoFallDamage} 额外 {Equipment.Current.ExtraFall} 翅膀 {TerraMovement.HasWings} 测试免摔 {mod.DevNoFallDamage} 无敌 {PlayerData.instance.isInvincible}");
                if (slide) NetCheck(lost == 0, $"滑墙滑到底落地不摔伤：掉血 {lost}");
                else NetCheck(lost > 0, $"对照：墙顶直接掉下 30 格摔伤 {lost}（原版 (30-25)×10 = 50 再减防御的一半）");
                yield return new WaitForSeconds(0.5f);
            }
            foreach (var c in wall) TerraWorld.Set(c.x, c.y, CellKind.Empty);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            mod.Stats.FullHeal();
            yield return new WaitForSeconds(0.5f);
        }
    }
}
