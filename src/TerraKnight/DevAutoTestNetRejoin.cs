using System.Collections;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "netrejoin", hosting with the in-process bot): a LAN game's death (Net/NetDeath.cs) and the way back into a
    /// boss fight (Net/BossRejoin.cs). In the False Knight's arena by its seal, the bot said to stand in the same room a few
    /// units off; the Knight killed: while the respawn waits, the corpse (the lying pose, half seen, nothing worn) is what is
    /// drawn and sent, its seconds counted, the inventory kept shut; about 15 s on, up at the bench first; then taken to the
    /// bot in the room, invincible as control returns there, the respawn point the bench as before. The bot gone from the
    /// room and the Knight killed again: the bench, and no trip. Close-ups netrejoin_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NetRejoinTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 联机重返 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            yield return new WaitForSeconds(1f);
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            inv.Selected = 0;
            inv.Slots[0] = ItemStack.Of(ItemType.BossSigilFalseKnight, 3);
            Bestiary.Kills["FalseKnight"] = 1;
            mod.DevUseSelected(HeroController.instance.transform.position);
            float until = Time.time + 30f;
            while (Time.time < until && !(BossArena.Arrived("FalseKnight") && GameHooks.PlayerHasControl())) yield return null;
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            string room = NetScenes.Current;
            Check("进了假骑士的竞技场", BossArena.Arrived("FalseKnight"), $"场景 {GameManager.instance.sceneName}，所在房间 {room}");
            string bench = pd.respawnScene, benchMarker = pd.respawnMarkerName;
            Vector2 Local(Vector3 world) { var r = World.TerrainSystem.Find(room); return r != null && r.Root != null ? (Vector2)r.Root.InverseTransformPoint(world) : (Vector2)world; }
            Vector2 World2(Vector2 local) { var r = World.TerrainSystem.Find(room); return r != null && r.Root != null ? (Vector2)r.Root.TransformPoint(local) : local; }
            Vector2 local = Local(hero.transform.position + Vector3.right * 3f);

            // The bot in the room, a few units off, alive.
            string botScene = room;
            bool keep = true;
            IEnumerator Keep()
            {
                while (keep)
                {
                    var s = new PlayerState { Scene = botScene, Feet = botScene == room ? local : Vector2.zero, Shown = true, Grounded = true, Life = 100, LifeMax = 100 };
                    var p = bot.Mine(Msg.PlayerState);
                    s.Write(p.W);
                    bot.Send(p);
                    bot.Scenes(new[] { botScene }, new string[0]);
                    bot.Pump();
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            mod.StartCoroutine(Keep());
            yield return new WaitForSeconds(1.5f);
            RemotePlayer mate = null;
            foreach (var rp in NetSession.Players.Values) if (rp.Slot == bot.Slot) mate = rp;
            Check("机器人在同一个房间", mate != null && mate.HasState && mate.Latest.Scene == room, $"机器人 {(mate != null && mate.HasState ? mate.Latest.Scene + " " + mate.Latest.Feet.ToString("F1") : "没有状态")}");

            // Killed: the corpse while the respawn waits.
            int armed0 = BossRejoin.DevArmed, travels0 = BossRejoin.DevTravels, rejoins0 = BossRejoin.DevRejoins;
            pd.isInvincible = false;
            mod.DevNoFallDamage = false;
            mod.Stats.TakeDirect(999999);
            hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
            float diedAt = Time.time;
            until = Time.time + 10f;
            while (!hero.cState.dead && Time.time < until) yield return null;
            yield return new WaitForSeconds(4f);
            int lying = Player.HeroSpecialAnimations.Encode(1, 0);
            var pose = mod.NetPose;
            int left = NetDeath.SecondsLeft;
            Items.InventoryGui.Open = true;
            yield return null;
            bool shut = !Items.InventoryGui.Open;
            Shot(GameManager.instance.sceneName, "netrejoin_corpse");
            Check("倒下后是半透明、不带装备的尸体", NetDeath.ShowCorpse && pose.Shown && pose.BodyOverride == lying && Mathf.Abs(pose.Opacity - NetDeath.CorpseOpacity) < 0.01f
                && pose.Equips.Head == 0 && pose.Equips.Body == 0 && pose.Item == ItemType.None,
                $"尸体 {NetDeath.ShowCorpse}，姿势 {pose.BodyOverride}（趴地 {lying}），透明度 {pose.Opacity:F2}，头/身 {pose.Equips.Head}/{pose.Equips.Body}，手持 {pose.Item}");
            Check("倒计时、背包打不开", left >= 9 && left <= 12 && shut, $"4 秒后还剩 {left} 秒，背包被关上 {shut}");
            Check("记下要回首领房", BossRejoin.DevArmed > armed0, $"{BossRejoin.DevArmed - armed0} 次");

            // The respawn: the bench first, about 15 s after the death.
            // The first moment of control after the death, wherever it is (the trip back starts at once after it).
            until = Time.time + 40f;
            float respawnAfter = -1f;
            string firstScene = null;
            while (Time.time < until && firstScene == null)
            {
                hero = HeroController.instance;
                if (hero != null && !hero.cState.dead && !GameManager.instance.IsInSceneTransition && GameHooks.PlayerHasControl())
                {
                    respawnAfter = Time.time - diedAt;
                    firstScene = GameManager.instance.sceneName;
                }
                else yield return null;
            }
            Check("先在复活点站起来", firstScene == bench && respawnAfter >= 14.5f && respawnAfter <= 22f, $"{respawnAfter:F1} 秒后在 {firstScene} 能动了（长椅 {bench}）");

            // Then to the bot in the room.
            until = Time.time + 40f;
            while (Time.time < until && BossRejoin.DevRejoins == rejoins0) yield return null;
            yield return null;
            hero = HeroController.instance;
            bool invincible = hero.cState.invulnerable;
            float off = Vector2.Distance(hero.transform.position, World2(local));
            Check("传送是从复活点出发的", BossRejoin.DevFrom == bench, $"从 {BossRejoin.DevFrom} 出发");
            Check("然后被送回首领房、在队友身边", BossRejoin.DevTravels > travels0 && BossRejoin.DevRejoins > rejoins0 && NetScenes.Current == room && off < 3f,
                $"去了 {BossRejoin.DevTravels - travels0} 次，在 {NetScenes.Current}，离队友 {off:F2}，{BossRejoin.DevLast}");
            Check("回来时有 1 秒无敌", invincible, $"无敌 {invincible}");
            // The marker may be the world's spawn now (PylonsSpawnPoint: a save that hasn't rested on a bench yet); the scene is the respawn's.
            Check("复活点还是原来的场景", pd.respawnScene == bench && !pd.respawnMarkerName.StartsWith(World.Wormhole.MatePrefix), $"复活点 {pd.respawnScene} / {pd.respawnMarkerName}（原来 {bench} / {benchMarker}）");
            Shot(GameManager.instance.sceneName, "netrejoin_back");

            // The bot gone from the room: the bench, no trip.
            botScene = "Town";
            yield return new WaitForSeconds(1.5f);
            int travels1 = BossRejoin.DevTravels;
            string back = null;
            yield return DieAndComeBack((at, scene) => back = scene);
            yield return new WaitForSeconds(3f);
            Check("没有队友在房里时照常回长椅", BossRejoin.DevTravels == travels1 && back == bench && GameManager.instance.sceneName == bench, $"回到 {back}，现在在 {GameManager.instance.sceneName}（长椅 {bench}）");
            keep = false;
            inv.Infinite = infinite;
            Plugin.Log.LogInfo($"[AutoTest] 联机重返 结果 {ok}/{all}");
        }
    }
}
