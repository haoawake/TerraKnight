using System.Collections;
using System.Linq;
using HarmonyLib;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        // Native burrow FSM, hidden VFX and pool lifetime, followed by the reported summoner loadout.
        IEnumerator CarverFireTest()
        {
            void Check(string name, bool ok, string detail = "") =>
                Plugin.Log.LogInfo($"[AutoTest] CARVER_FIRE {name}: {detail} {(ok ? "通过" : "不通过")}");
            HealthManager[] Carvers(string room) => Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(h => h.gameObject.scene.name == room && UI.EnemyHover.RecordOf(h) == "BabyCentipede"
                    && !PathOf(h.transform).Contains("TK Enemy Templates")).ToArray();
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            try
            {
                yield return GoTo("Deepnest_01b", EntryGateOf("Deepnest_01b"));
                var hm = Carvers("Deepnest_01b").FirstOrDefault(h => h.gameObject.activeInHierarchy && !h.isDead);
                Check("找到原版刻尘者", hm != null);
                if (hm == null) yield break;
                var f = hm.GetComponents<PlayMakerFSM>().First(x => x.FsmName == "Centipede");
                f.SetState("Become Dormant");
                f.enabled = false;
                var renderer = hm.GetComponent<Renderer>();
                Check("原版地下状态全向无敌并隐藏", EnemyStats.Untouchable(hm) && renderer != null && !renderer.enabled);
                EnemyDebuffs.Clear();
                EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 600);
                int hp = hm.hp;
                int held = EnemyDebuffs.DevHeldOff;
                var nextDust = AccessTools.Field(typeof(TerraParticles), "nextDust");
                int dust = (int)nextDust.GetValue(TerraParticles.Instance);
                for (int i = 0; i < 120; i++) EnemyDebuffs.Tick();
                Check("地下燃烧不扣血", hm.hp == hp && !hm.isDead && EnemyDebuffs.DevHeldOff > held,
                    $"hp={hp}->{hm.hp}, blocked={EnemyDebuffs.DevHeldOff - held}");
                Check("地下不继续产生燃烧粒子", dust == (int)nextDust.GetValue(TerraParticles.Instance));

                // A disabled pooled enemy must lose its old debuffs, even if its invincibility flag was cleared.
                hm.IsInvincible = false;
                hm.gameObject.SetActive(false);
                EnemyDebuffs.Tick();
                Check("回收到对象池清除旧燃烧", !EnemyDebuffs.Has(hm, EnemyDebuffs.OnFire));
                EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 600);
                Check("停用对象不能重新附加燃烧", !EnemyDebuffs.Has(hm, EnemyDebuffs.OnFire));
                EnemyDebuffs.Clear();
                hm.gameObject.SetActive(true);
                f.enabled = false;
                hm.IsInvincible = false;
                renderer.enabled = true;
                hp = hm.hp;
                float carry = EnemyStats.Of(hm).Carry;
                EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 600);
                dust = (int)nextDust.GetValue(TerraParticles.Instance);
                for (int i = 0; i < 120; i++) EnemyDebuffs.Tick();
                Check("地表燃烧照常扣血并产生粒子", (hm.hp < hp || EnemyStats.Of(hm).Carry > carry)
                    && dust != (int)nextDust.GetValue(TerraParticles.Instance));
                EnemyDebuffs.Clear();

                foreach (var room in new[] { "Deepnest_01b", "Deepnest_26" })
                {
                    // Reload the first room as well: the pool lifetime probe above deliberately stopped its FSM.
                    yield return GoTo("Town", "left1");
                    yield return GoTo(room, EntryGateOf(room));
                    mod.ApplyCreative(false);
                    Equip(new[] { ItemType.ObsidianOutlawHat, ItemType.ObsidianLongcoat, ItemType.ObsidianPants }, new ItemType[0]);
                    Minions.Instance.Clear();
                    PlayerBuffs.Clear();
                    SummonTags.Clear();
                    EnemyDebuffs.Clear();
                    Whips.Instance.Clear();
                    Minions.Instance.Summon(ItemDb.Get(ItemType.ImpStaff), hero.transform.position);
                    Minions.Instance.Summon(ItemDb.Get(ItemType.ImpStaff), hero.transform.position);
                    var inv = mod.Inventory;
                    inv.Selected = 0;
                    inv.Slots[0] = ItemStack.Of(ItemType.Firecracker);
                    int start = Carvers(room).Length, peak = start, latePeak = 0;
                    int hits = Whips.DevStrikes, minionHits = Minions.DevHits, blasts = Minions.DevBlasts;
                    float until = Time.time + 45f, moveAt = 0;
                    while (Time.time < until)
                    {
                        var all = Carvers(room);
                        peak = Mathf.Max(peak, all.Length);
                        if (Time.time > until - 15f) latePeak = Mathf.Max(latePeak, all.Length);
                        var candidates = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                            .Where(h => h.gameObject.scene.name == room && !h.isDead && !ArenaGuard.OffMap(h)
                                && (UI.EnemyHover.RecordOf(h) == "BabyCentipede" || UI.EnemyHover.RecordOf(h) == "CentipedeHatcher"))
                            .OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).ToArray();
                        var target = candidates.FirstOrDefault(h => !EnemyStats.Untouchable(h)) ?? candidates.FirstOrDefault();
                        if (target != null)
                        {
                            if (Time.time >= moveAt)
                            {
                                hero.transform.position = Clear(hero, (Vector2)target.transform.position + Vector2.right * 2f);
                                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                                moveAt = Time.time + 2f;
                            }
                            if (mod.DevReady) mod.DevUseSelected(target.transform.position);
                        }
                        yield return new WaitForSeconds(0.1f);
                    }
                    Plugin.Log.LogInfo($"[AutoTest] CARVER_FIRE COMBAT {room}: start={start}, peak={peak}, last15sPeak={latePeak}, end={Carvers(room).Length}, whipHits={Whips.DevStrikes - hits}, minionHits={Minions.DevHits - minionHits}, blasts={Minions.DevBlasts - blasts}");
                    Check(room + "实际鞭炮与小鬼命中", Whips.DevStrikes > hits && Minions.DevHits > minionHits && Minions.DevBlasts > blasts);
                    Minions.Instance.Clear();
                }
            }
            finally
            {
                EnemyDebuffs.Clear();
                TerraStats.DevImmortal = false;
                mod.DevNoFallDamage = false;
            }
        }
    }
}
