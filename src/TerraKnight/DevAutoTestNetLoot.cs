using System.Collections;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suites "netloothost" and "netlootclient": a LAN game's loot between two copies of the game (port 7797), as
    /// Terraria's NPCLoot on the server: a boss's loot rolled in the game that runs the enemy lies in both worlds, one of
    /// each, drawn; the same roll in the other game (its puppet's death) makes none of it but the player's own treasure
    /// bag; a real kill likewise; a reforged, forged sword thrown keeps its prefix and level on the other side and when
    /// picked up there, and is gone from the thrower's world.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int NetLootPort = 7797;

        IEnumerator NetLootBoot()
        {
            if (suite == "netloothost")
            {
                bool ok = NetSession.Host(NetLootPort, "", 8, out string error);
                NetCheck(ok, $"掉落 开服 {NetLootPort} {error}");
                for (float t = 0f; t < 10f && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                yield break;
            }
            float until = Time.realtimeSinceStartup + 120f;
            while (!NetSession.Active && Time.realtimeSinceStartup < until)
            {
                NetSession.Join("127.0.0.1", NetLootPort);
                for (float t = 0f; t < 6f && NetSession.State != NetSession.Phase.Off && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                if (!NetSession.Active) yield return new WaitForSecondsRealtime(3f);
            }
            NetCheck(NetSession.Active, $"掉落 客户端加入主机（槽位 {NetSession.MySlot}）");
        }

        /// <summary>The loot lying now: shared (a network key) or this game's own, treasure bags apart.</summary>
        static (int Shared, int Own, int Bags, int Hidden) LootCount()
        {
            int shared = 0, own = 0, bags = 0, hidden = 0;
            foreach (var (netId, stack, _, drawn) in WorldItems.DevItems)
            {
                if (stack.Type.ToString().StartsWith("Bag")) { bags++; continue; }
                if (netId != null) shared++; else own++;
                if (!drawn) hidden++;
            }
            return (shared, own, bags, hidden);
        }

        IEnumerator NetLootHost(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            WorldItems.DevNoGrab = true;
            var col = hero.GetComponent<BoxCollider2D>();
            RemotePlayer other = null;
            for (float t = 0f; t < 180f; t += Time.unscaledDeltaTime)
            {
                other = Other();
                if (other != null && other.Visible) break;
                yield return null;
            }
            NetCheck(other != null && other.Visible, $"掉落 看到客户端：{(other != null ? other.Name : "没有")}");
            NetCheck(NetSession.Owners.TryGetValue(scene, out int owner) && owner == NetSession.MySlot, $"掉落 {scene} 的敌人归主机（{owner}）");
            NetChat.Say("loot: host ready");
            yield return WaitChat("loot: client ready", 120f);
            yield return new WaitForSeconds(1f);

            // 1. A boss's loot, rolled here (the game that runs the enemy): into both worlds.
            var before = LootCount();
            Relics.ContextScene = scene;
            EnemyDrops.Roll("FalseKnight", (Vector2)col.bounds.center + new Vector2(3f, 1f));
            Relics.ContextScene = null;
            yield return new WaitForSeconds(1.5f);
            var after = LootCount();
            NetCheck(after.Shared > before.Shared && after.Own == before.Own,
                $"掉落 主机掷首领掉落：共享 {after.Shared - before.Shared} 个，只在主机的 {after.Own - before.Own} 个，宝藏袋 {after.Bags - before.Bags}，没画出来 {after.Hidden}");
            Shot(scene, "netloot_host_1_roll");
            yield return null;
            NetChat.Say($"loot: host rolled {after.Shared - before.Shared}");
            yield return WaitChat("loot: client checked roll", 60f);

            // 2. A real kill: the client's puppet dies with it.
            before = LootCount();
            var target = NearestEnemy(hero.transform.position, false);
            string targetName = target != null ? target.name : "没有";
            if (target != null)
            {
                var tcol = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
                for (int i = 0; i < 40 && target != null && !target.isDead && tcol != null; i++)
                {
                    mod.Melee.HitWith(tcol, 50, 0f, 0f, 1f, col.bounds.center);
                    yield return new WaitForSeconds(0.12f);
                }
            }
            yield return new WaitForSeconds(1.5f);
            after = LootCount();
            NetCheck(target == null || target.isDead, $"掉落 主机打死 {targetName}：共享 +{after.Shared - before.Shared}，只在主机的 +{after.Own - before.Own}（死亡发出 {Net.Enemies.DevDeathsSent}）");
            NetChat.Say("loot: host killed");
            yield return WaitChat("loot: client checked kill", 60f);

            // 3. A reforged, forged sword thrown.
            var sword = ItemStack.Of(ItemType.GoldBroadsword);
            sword.Prefix = 81;
            sword.Forge = 3;
            WorldItems.Drop(sword, (Vector2)col.bounds.center + new Vector2(-3f, 1f));
            yield return new WaitForSeconds(0.5f);
            NetChat.Say("loot: host threw");
            yield return WaitChat("loot: client picked", 90f);
            yield return new WaitForSeconds(1.5f);
            bool still = false;
            foreach (var (_, stack, _, _) in WorldItems.DevItems) if (stack.Type == ItemType.GoldBroadsword) still = true;
            NetCheck(!still, "掉落 客户端捡走的剑在主机这边也没了");
            Shot(scene, "netloot_host_2_end");
            yield return null;
            NetChat.Say("loot: host bye");
            yield return new WaitForSeconds(2f);
            WorldItems.DevNoGrab = false;
        }

        IEnumerator NetLootClient(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            WorldItems.DevNoGrab = true;
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < 90f && !(NetSession.Owners.TryGetValue(scene, out int o) && o != NetSession.MySlot && Net.Enemies.DevPuppets > 0); t += Time.unscaledDeltaTime) yield return null;
            NetCheck(Net.Enemies.RemoteOwned(scene) && Net.Enemies.DevPuppets > 0, $"掉落 主机的敌人在这边是傀儡：{Net.Enemies.DevReport()}");
            yield return WaitChat("loot: host ready", 120f);
            NetChat.Say("loot: client ready");

            // 1. The host's roll comes over; the same roll here (as the puppet's death would make it) makes none.
            var before = LootCount();
            yield return WaitChat("loot: host rolled", 60f);
            yield return new WaitForSeconds(2f);
            var came = LootCount();
            NetCheck(came.Shared > before.Shared && came.Hidden == 0, $"掉落 主机掷的首领掉落传过来：共享 +{came.Shared - before.Shared}，没画出来 {came.Hidden}");
            int skipped0 = EnemyDrops.DevPuppetSkipped;
            Relics.ContextScene = scene;
            EnemyDrops.Roll("FalseKnight", (Vector2)col.bounds.center + new Vector2(-3f, 1f));
            Relics.ContextScene = null;
            yield return new WaitForSeconds(1f);
            var mine = LootCount();
            NetCheck(mine.Own == came.Own && EnemyDrops.DevPuppetSkipped > skipped0,
                $"掉落 同样的掷骰在傀儡这边：自己世界多了 {mine.Own - came.Own} 个（应为 0），跳过 {EnemyDrops.DevPuppetSkipped - skipped0} 样，宝藏袋 +{mine.Bags - came.Bags}（每人一个）");
            Shot(scene, "netloot_client_1_roll");
            yield return null;
            NetChat.Say("loot: client checked roll");

            // 2. The host's real kill: the puppet dies here, nothing of its loot made here.
            before = LootCount();
            yield return WaitChat("loot: host killed", 90f);
            yield return new WaitForSeconds(2f);
            var kill = LootCount();
            NetCheck(kill.Own == before.Own, $"掉落 主机打死的敌人：傀儡死时这边自己多了 {kill.Own - before.Own} 个（应为 0），共享 +{kill.Shared - before.Shared}，收到死亡 {Net.Enemies.DevDeathsReceived}");
            NetChat.Say("loot: client checked kill");

            // 3. The host's sword: its prefix and level here, and picked up with them.
            yield return WaitChat("loot: host threw", 60f);
            Vector2? swordAt = null;
            ItemStack seen = default;
            for (float t = 0f; t < 5f && swordAt == null; t += Time.deltaTime)
            {
                foreach (var (netId, stack, at, drawn) in WorldItems.DevItems)
                    if (netId != null && stack.Type == ItemType.GoldBroadsword) { swordAt = at; seen = stack; }
                yield return null;
            }
            NetCheck(swordAt != null && seen.Prefix == 81 && seen.Forge == 3, $"掉落 主机扔的剑在这边：前缀 {seen.Prefix}（81），锻造 {seen.Forge}（3）");
            if (swordAt != null)
            {
                WorldItems.DevNoGrab = false;
                hero.transform.position = Clear(hero, swordAt.Value + new Vector2(0f, 0.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                ItemStack got = default;
                for (float t = 0f; t < 6f && got.IsEmpty; t += Time.deltaTime)
                {
                    foreach (var s in mod.Inventory.Slots) if (!s.IsEmpty && s.Type == ItemType.GoldBroadsword) got = s;
                    yield return null;
                }
                NetCheck(!got.IsEmpty && got.Prefix == 81 && got.Forge == 3, $"掉落 捡起来进背包：{(got.IsEmpty ? "没捡到" : $"前缀 {got.Prefix} 锻造 {got.Forge}")}");
            }
            NetChat.Say("loot: client picked");
            yield return WaitChat("loot: host bye", 60f);
            WorldItems.DevNoGrab = false;
        }
    }
}
