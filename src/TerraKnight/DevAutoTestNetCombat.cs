using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        HealthManager NetCombatFixture(string scene, HeroController hero, bool host)
        {
            var container = new GameObject("TK Net Combat Container");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(container, NetScenes.FindScene(scene));
            container.transform.position = hero.transform.position + Vector3.up * 12f;
            if (!host) container.SetActive(false);
            var body = new GameObject("TK Net Combat Body");
            body.transform.SetParent(container.transform, false);
            var hm = body.AddComponent<HealthManager>();
            hm.hp = 500;
            EnemyStats.Of(hm).Boss = true;
            var source = Object.FindObjectsByType<tk2dBaseSprite>(FindObjectsSortMode.None).FirstOrDefault(s => s.Collection != null);
            var sprite = body.AddComponent<tk2dSprite>();
            if (source != null) sprite.SetSprite(source.Collection, source.spriteId);
            sprite.GetComponent<Renderer>().enabled = host;
            var child = new GameObject("Attack Sprite");
            child.transform.SetParent(body.transform, false);
            var part = child.AddComponent<tk2dSprite>();
            if (source != null) part.SetSprite(source.Collection, source.spriteId);
            child.transform.localPosition = new Vector3(1f, 2f, 0f);
            child.SetActive(host);
            return hm;
        }

        IEnumerator NetCombatHost(string scene, HeroController hero)
        {
            yield return WaitChat("combat: fixture ready", 30f);
            var fixture = NetCombatFixture(scene, hero, true);
            yield return WaitChat("combat: fixture visible", 30f);
            NetChat.Say("combat: prepare hurt");
            yield return WaitChat("combat: hurt ready", 30f);
            var attack = new GameObject("TK Net Combat Shot");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(attack, NetScenes.FindScene(scene));
            var collider = attack.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2f, 3f);
            collider.isTrigger = true;
            var damage = attack.AddComponent<DamageHero>();
            damage.damageDealt = 1; damage.hazardType = 1;
            // A shot with no HealthManager, placed at the remote player's current position.
            for (float t = 0f; t < 8f && !NetChat.DevHas("combat: hurt received"); t += Time.unscaledDeltaTime)
            {
                var other = Other();
                if (other != null && NetScenes.ToWorld(other.Latest.Scene, other.Latest.BoxCenter, out var at)) attack.transform.position = at;
                yield return null;
            }
            Object.Destroy(attack);
            NetCheck(NetChat.DevHas("combat: hurt received"), "无HealthManager的敌人攻击在客机实际扣血");
            NetChat.Say("combat: attack removed");
            yield return WaitChat("combat: cleared", 30f);
            Object.Destroy(fixture.transform.parent.gameObject);
            yield return null;
        }

        IEnumerator NetCombatClient(string scene, HeroController hero)
        {
            var fixture = NetCombatFixture(scene, hero, false);
            NetChat.Say("combat: fixture ready");
            yield return NetWorldWait(() => fixture != null && Enemies.IsPuppet(fixture) && fixture.gameObject.activeInHierarchy
                && fixture.GetComponent<Renderer>().enabled && fixture.transform.GetChild(0).gameObject.activeInHierarchy,
                "快照激活Boss父容器、根精灵及被本地关闭的子部件", 30f);
            NetChat.Say("combat: fixture visible");
            NetCombatUnmatched(scene);
            yield return WaitChat("combat: prepare hurt", 30f);
            PlayerData.instance.isInvincible = false;
            bool immortal = Player.TerraStats.DevImmortal;
            Player.TerraStats.DevImmortal = true;
            mod.Stats.FullHeal();
            int before = mod.Stats.Life, hits = EnemyAttacks.DevHits;
            Plugin.Log.LogInfo($"[AutoTest] combat hurt before Life={before}/{mod.Stats.LifeMax}, dead={hero.cState.dead}, native={PlayerData.instance.health}");
            NetChat.Say("combat: hurt ready");
            yield return NetWorldWait(() => mod.Stats.Life < before && EnemyAttacks.DevHits > hits,
                "客机触碰主机生成的独立攻击后实际扣血", 12f);
            NetCheck(mod.Stats.Life > 0 && !hero.cState.dead, "伤害回归保留存活状态继续验证Boss入场");
            Plugin.Log.LogInfo($"[AutoTest] combat hurt after Life={mod.Stats.Life}, dead={hero.cState.dead}, native={PlayerData.instance.health}");
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = immortal;
            EnemyAttacks.DevDestroyGhostObjects();
            yield return null;
            yield return null;
            yield return NetWorldWait(() => EnemyAttacks.DevLiveGhosts > 0, "场景对象销毁后下一次攻击快照重建判定体", 3f);
            NetChat.Say("combat: hurt received");
            mod.Stats.FullHeal();
            yield return WaitChat("combat: attack removed", 30f);
            yield return new WaitForSecondsRealtime(0.5f);
            // Scene frame completion removes shots whose colliders stopped being active.
            NetCheck(EnemyAttacks.DevReceived > 0, "攻击形状通过真实网络连接同步");
            NetChat.Say("combat: cleared");
            Object.Destroy(fixture.transform.parent.gameObject);
            PlayerData.instance.isInvincible = true;
        }

        IEnumerator NetCombatBossHost()
        {
            PlayerData.instance.isInvincible = true;
            NetCheck(BossArena.Fight("Hornet"), "主机进入黄蜂真实战场");
            string scene = BossArena.SceneOf("Hornet");
            yield return NetWorldWait(() => GameManager.instance.sceneName == scene && !GameManager.instance.IsInSceneTransition,
                "主机完成黄蜂战场加载", 45f);
            NetCombatEnterArena(scene);
            yield return NetWorldWait(() => EnemyStats.All.Any(i => i.Hm != null && i.Record == "Hornet" && i.Hm.gameObject.activeInHierarchy && !i.Hm.isDead),
                "主机进入战斗范围并触发黄蜂出场", 30f);
            NetChat.Say("combat: boss enter");
            yield return WaitChat("combat: boss visible", 60f);
            NetCheck(EnemyStats.All.Any(i => i.Hm != null && i.Record == "Hornet" && i.Hm.gameObject.activeInHierarchy && !i.Hm.isDead),
                "双人进入后主机黄蜂仍然存在");
            NetChat.Say("combat: boss done");
        }

        IEnumerator NetCombatBossClient()
        {
            yield return WaitChat("combat: boss enter", 60f);
            PlayerData.instance.isInvincible = true;
            yield return NetWorldWait(() => !heroBusy(), "客机准备好切换Boss战场", 30f);
            NetCheck(BossArena.Fight("Hornet"), "客机进入同一黄蜂真实战场");
            string scene = BossArena.SceneOf("Hornet");
            yield return NetWorldWait(() => GameManager.instance.sceneName == scene && !GameManager.instance.IsInSceneTransition,
                "客机完成黄蜂战场加载", 45f);
            NetCombatEnterArena(scene);
            yield return NetWorldWait(() => EnemyStats.All.Any(i => i.Record == "Hornet" && i.Hm != null && !i.Hm.isDead
                && Enemies.IsPuppet(i.Hm) && i.Hm.gameObject.activeInHierarchy && i.Hm.GetComponent<Renderer>()?.enabled == true),
                "客机能看到主机同步的真实黄蜂Boss", 30f);
            NetChat.Say("combat: boss visible");
            yield return WaitChat("combat: boss done", 30f);
        }
        static bool heroBusy() => GameManager.instance.IsInSceneTransition || HeroController.instance.cState.dead;
        static void NetCombatUnmatched(string scene)
        {
            var local = new GameObject("TK Net Unmatched Local Boss");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(local, NetScenes.FindScene(scene));
            var hm = local.AddComponent<HealthManager>(); hm.hp = 500;
            using (var stream = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(stream))
            {
                w.Write(scene); w.Write((ushort)1);
                w.Write(int.MaxValue); w.Write((byte)11); w.Write("Missing Boss@999");
                w.Write(0f); w.Write(0f); w.Write(1f); w.Write(1f); w.Write(0f);
                w.Write((short)-1); w.Write((byte)0); w.Write(500); w.Write((ushort)0);
                stream.Position = 0;
                Enemies.ReceiveSnapshot(Other().Slot, new System.IO.BinaryReader(stream));
                NetCheck(local.activeSelf, "无法匹配远端Boss时不误隐藏本地Boss");
            }
            Object.Destroy(local);
        }
        static void NetCombatEnterArena(string scene)
        {
            var region = World.TerrainSystem.Find(scene);
            if (region?.OwnZone == null) return;
            var zone = region.OwnZone.Value;
            var hero = HeroController.instance;
            hero.transform.position = new Vector3(zone.center.x, zone.yMin + 3f, hero.transform.position.z);
        }
    }
}
