using System.Collections;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suites "net2host" and "net2client": a LAN game between two copies of the game on this
    /// computer (the second one a <see cref="DevCopy"/> with its own saves). The host starts a
    /// server and a new game in King's Pass; the client joins and starts its own new game there.
    /// They take turns by chat lines: the client finds the host's enemies as puppets and hits one
    /// (the host's game deals it), the host shoots and summons (the client sees the ghosts), both
    /// turn PvP on and the client swings at the host, the host kills an enemy (the client's puppet
    /// dies with it), the client digs a block (the host's world loses it).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int Net2Port = 7792;

        IEnumerator Net2Boot()
        {
            if (suite == "net2host" || suite == "networldhost")
            {
                bool ok = NetSession.Host(Net2Port, "", 8, out string error);
                NetCheck(ok, $"开服 {Net2Port} {error}");
                for (float t = 0f; t < 10f && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                NetCheck(NetSession.Active, "主机加入自己的服务器");
                yield break;
            }
            // The client: the host's copy may still be starting; try for two minutes.
            float until = Time.realtimeSinceStartup + 120f;
            while (!NetSession.Active && Time.realtimeSinceStartup < until)
            {
                NetSession.Join("127.0.0.1", Net2Port);
                for (float t = 0f; t < 6f && NetSession.State != NetSession.Phase.Off && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                if (!NetSession.Active)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 联机2 还连不上主机（{NetSession.Ended}），再试");
                    yield return new WaitForSecondsRealtime(3f);
                }
            }
            NetCheck(NetSession.Active, $"客户端加入主机（槽位 {NetSession.MySlot}）");
        }

        IEnumerator WaitChat(string text, float seconds)
        {
            for (float t = 0f; t < seconds && !NetChat.DevHas(text); t += Time.unscaledDeltaTime) yield return null;
            if (!NetChat.DevHas(text)) Plugin.Log.LogWarning($"[AutoTest] 联机2 等「{text}」超时 [NET_TIMEOUT]");
        }

        static RemotePlayer Other()
        {
            foreach (var p in NetSession.Players.Values) return p;
            return null;
        }

        static HealthManager NearestEnemy(Vector2 at, bool puppet)
        {
            HealthManager best = null;
            float d = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || Net.Enemies.IsPuppet(hm) != puppet) continue;
                float dd = Vector2.Distance(hm.transform.position, at);
                if (dd < d)
                {
                    d = dd;
                    best = hm;
                }
            }
            return best;
        }

        IEnumerator Net2Host(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = false;
            var col = hero.GetComponent<BoxCollider2D>();
            // The client's character arrives in King's Pass.
            RemotePlayer other = null;
            for (float t = 0f; t < 180f; t += Time.unscaledDeltaTime)
            {
                other = Other();
                if (other != null && other.Visible) break;
                yield return null;
            }
            NetCheck(other != null && other.Visible, $"看到客户端的人物：{(other != null ? other.Name + " " + other.Feet.ToString("F1") : "没有")}，主角 {hero.transform.position:F1}");
            NetCheck(NetSession.Owners.TryGetValue(scene, out int owner) && owner == NetSession.MySlot, $"国王山道的敌人归主机（{owner}，自己 {NetSession.MySlot}）");
            yield return new WaitForSeconds(1f);
            Shot(scene, "net2_host_1_both");
            yield return null;
            NetChat.Say("host: ready");
            yield return WaitChat("client: puppets ok", 60f);
            yield return new WaitForSeconds(1f);
            NetCheck(Net.Enemies.DevApplied > 0, $"客户端打傀儡的伤害在主机结算：{Net.Enemies.DevReport()}");

            // Shots and a minion for the client to see.
            mod.DevUse(ItemType.SlimeStaff, (Vector2)col.bounds.center + Vector2.right * 2f);
            for (int i = 0; i < 12; i++)
            {
                mod.DevUse(ItemType.WoodenBow, (Vector2)col.bounds.center + new Vector2(8f, 3f));
                yield return new WaitForSeconds(0.15f);
            }
            NetChat.Say("host: shots");
            yield return new WaitForSeconds(1f);
            Shot(scene, "net2_host_2_shots");
            yield return null;

            // PvP: the client swings at the host.
            yield return WaitChat("client: pvp", 60f);
            NetSession.SetPvp(true);
            mod.Stats.FullHeal();
            int life0 = mod.Stats.Life;
            NetChat.Say("host: pvp on");
            for (float t = 0f; t < 15f && mod.Stats.Life >= life0; t += Time.deltaTime) yield return null;
            NetCheck(mod.Stats.Life < life0, $"PvP：客户端砍到主机 {life0}->{mod.Stats.Life}（{PvpHits.DevHits} 下）");
            NetSession.SetPvp(false);
            mod.Stats.FullHeal();
            yield return new WaitForSeconds(1.5f);

            // An enemy killed here: the client's puppet of it dies too.
            var target = NearestEnemy(hero.transform.position, false);
            if (target != null)
            {
                var tcol = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
                string targetName = target.name;
                for (int i = 0; i < 40 && target != null && !target.isDead && tcol != null; i++)
                {
                    mod.Melee.HitWith(tcol, 50, 0f, 0f, 1f, col.bounds.center);
                    yield return new WaitForSeconds(0.12f);
                }
                // A splatter death destroys it.
                NetCheck(target == null || target.isDead, $"主机打死 {targetName}（发出死亡 {Net.Enemies.DevDeathsSent}）");
            }
            NetChat.Say("host: killed");
            yield return WaitChat("client: done", 90f);
            yield return new WaitForSeconds(1f);
            NetCheck(TileSync.DevApplied > 0, $"客户端挖的方块在主机这边也没了：收到 {TileSync.DevApplied} 格");
            Shot(scene, "net2_host_3_end");
            yield return new WaitForSeconds(0.5f);
            NetChat.Say("host: bye");
            yield return new WaitForSeconds(2f);
        }

        IEnumerator Net2Client(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = false;
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < 60f && !(NetSession.Owners.TryGetValue(scene, out int o) && o != NetSession.MySlot && Net.Enemies.DevPuppets > 0); t += Time.unscaledDeltaTime) yield return null;
            NetCheck(Net.Enemies.DevPuppets > 0, $"主机的敌人在这边是傀儡：{Net.Enemies.DevReport()}");
            var host = Other();
            NetCheck(host != null && host.Visible, $"看到主机的人物：{(host != null ? host.Name + " " + host.Feet.ToString("F1") : "没有")}，主角 {hero.transform.position:F1}");
            yield return WaitChat("host: ready", 60f);
            yield return new WaitForSeconds(1f);
            Shot(scene, "net2_client_1_both");
            yield return null;
            var pup = NearestEnemy(hero.transform.position, true);
            if (pup != null)
            {
                Vector2 a = pup.transform.position;
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 联机2 傀儡 {pup.name}：一秒内从 {a:F2} 到 {(Vector2)pup.transform.position:F2}，脚本全关 {Net.Enemies.DevScriptsOff(pup)}，血 {pup.hp}");
                var pcol = pup.GetComponent<Collider2D>() ?? pup.GetComponentInChildren<Collider2D>();
                if (pcol != null) mod.Melee.HitWith(pcol, 10, 2f, 0f, 1f, col.bounds.center);
                yield return new WaitForSeconds(0.2f);
                NetCheck(Net.Enemies.DevForwarded > 0, $"打傀儡 {pup.name}：伤害交给主机（{Net.Enemies.DevForwarded} 次）");
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 联机2 傀儡 {pup.name} 打完一秒后血 {pup.hp}（主机那边算过的）");
            }
            NetChat.Say("client: puppets ok");
            // The host's shots and its minion, drawn here.
            int ghosts = 0, minions = 0;
            for (float t = 0f; t < 20f && !NetChat.DevHas("host: shots"); t += Time.deltaTime)
            {
                ghosts = Mathf.Max(ghosts, TerraProjectiles.Instance.DevGhosts);
                minions = Mathf.Max(minions, Minions.Instance.DevGhosts);
                yield return null;
            }
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                ghosts = Mathf.Max(ghosts, TerraProjectiles.Instance.DevGhosts);
                minions = Mathf.Max(minions, Minions.Instance.DevGhosts);
                if (t > 0.5f && t - Time.deltaTime <= 0.5f) Shot(scene, "net2_client_2_ghosts");
                yield return null;
            }
            NetCheck(ghosts > 0 && minions > 0, $"主机的箭 {ghosts} 支、召唤物 {minions} 个画在这边");

            // PvP: both on, a sword swung at the host.
            NetSession.SetPvp(true);
            NetChat.Say("client: pvp");
            yield return WaitChat("host: pvp on", 30f);
            yield return new WaitForSeconds(0.5f);
            for (float t = 0f; t < 12f && PvpHitsLanded() == 0; t += 0.5f)
            {
                host = Other();
                if (host != null && host.Visible)
                {
                    // Stand next to the host and swing at them.
                    hero.transform.position = new Vector3(host.Feet.x - 1.2f, host.Feet.y + 0.1f + (hero.transform.position.y - col.bounds.min.y), hero.transform.position.z);
                    mod.DevUse(ItemType.CopperBroadsword, host.Center);
                }
                yield return new WaitForSeconds(0.5f);
            }
            NetSession.SetPvp(false);
            Plugin.Log.LogInfo($"[AutoTest] 联机2 PvP：收到主机受伤 {PvpHitsLanded()} 次");

            // The host kills an enemy; the puppet here dies with it.
            yield return WaitChat("host: killed", 60f);
            yield return new WaitForSeconds(1f);
            NetCheck(Net.Enemies.DevDeathsReceived > 0, $"主机打死的敌人在这边也死了：{Net.Enemies.DevReport()}");
            Shot(scene, "net2_client_3_after_kill");
            yield return null;

            // A block dug here goes from the host's world too.
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.1f));
            TerraWorld.Set(feet.x + 3, feet.y, CellKind.Empty);
            TerraWorld.Set(feet.x + 3, feet.y - 1, CellKind.Empty);
            yield return new WaitForSeconds(0.5f);
            NetCheck(TileSync.DevSent > 0, $"挖掉的方块发给主机 {TileSync.DevSent} 格");
            NetChat.Say("client: done");
            yield return WaitChat("host: bye", 30f);
            yield return new WaitForSeconds(1f);
        }

        /// <summary>PlayerHurt messages about the host this game has had.</summary>
        static int PvpHitsLanded() => NetSession.DevReceived.TryGetValue(Msg.PlayerHurt, out int n) ? n : 0;
    }
}
