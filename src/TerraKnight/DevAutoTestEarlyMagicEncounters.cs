using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator EarlyMagicEncounters(ItemDef[] weapons)
        {
            foreach (var room in new[] { (0, "Crossroads_01"), (1, "Fungus2_03"), (2, "Ruins1_01") })
            {
                if (!weapons.Any(d => d.Stage == room.Item1)) continue;
                Shop.DevStage = room.Item1;
                yield return GoTo(room.Item2, EntryGateOf(room.Item2));
                yield return new WaitForSeconds(1);
                var originals = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && h.gameObject.activeInHierarchy && h.gameObject.scene.name == room.Item2 && !h.isDead).ToArray();
                // Ceiling Droppers self-destruct and leave native gas explosions; they are
                // unsuitable as repeated single-target kill fixtures.
                var templates = originals.Where(h => EnemyStats.Of(h) is EnemyStats.Info i && !i.Boss && !i.Elite && i.Harmful && !string.IsNullOrEmpty(i.Record) && i.Record != "CeilingDropper")
                    .GroupBy(h => EnemyStats.Of(h).Record).Select(g => g.First()).OrderBy(h => EnemyStats.Of(h).Record).Take(2)
                    .Select(h => new { Go = h.gameObject, Record = EnemyStats.Of(h).Record, Hp = EnemyStats.Of(h).MaxHp }).ToArray();
                if (templates.Length == 0) throw new InvalidOperationException("No native enemy templates in " + room.Item2);
                foreach (var h in originals) h.gameObject.SetActive(false);
                var hero = HeroController.instance;
                var rb = hero.GetComponent<Rigidbody2D>();
                var hc = hero.GetComponent<Collider2D>();
                var stand = Clear(hero, hero.transform.position + Vector3.right * 3);
                try
                {
                    foreach (var ai in new[] { EnemyAiKind.Vanilla, EnemyAiKind.New })
                    foreach (var template in templates)
                    foreach (var d in weapons.Where(d => d.Stage == room.Item1))
                    {
                        EnemyAiMode.Current = ai;
                        Shop.DevStage = d.Stage;
                        EarlyMagicEquip(d, "finite");
                        mod.Stats.FullHeal();
                        PlayerData.instance.isInvincible = false;
                        hero.RegainControl(); hero.StartAnimationControl();
                        hero.transform.position = stand; rb.linearVelocity = Vector2.zero;
                        for (float until = Time.realtimeSinceStartup + 3; hero.cState.invulnerable && Time.realtimeSinceStartup < until;)
                            yield return new WaitForSecondsRealtime(.1f);
                        var clone = Object.Instantiate(template.Go);
                        var hm = clone.GetComponent<HealthManager>();
                        if (hm == null) { Object.Destroy(clone); throw new InvalidOperationException("Native clone has no HealthManager"); }
                        clone.name = template.Go.name;
                        clone.transform.position = hc.bounds.center + Vector3.right * 5;
                        clone.SetActive(true);
                        Physics2D.SyncTransforms();
                        var tc = clone.GetComponent<Collider2D>();
                        var ground = Physics2D.Raycast((Vector2)clone.transform.position + Vector2.up * 4, Vector2.down, 15, 1 << ChunkColliders.TerrainLayer);
                        var enemyBody = clone.GetComponent<Rigidbody2D>();
                        bool flying = enemyBody != null && Mathf.Abs(enemyBody.gravityScale) < .01f;
                        if (ground.collider != null && tc != null) clone.transform.position += Vector3.up * (ground.point.y - tc.bounds.min.y + (flying ? 1.5f : .1f));
                        hm.hp = template.Hp; hm.isDead = false;
                        clone.SetActive(true);
                        Physics2D.SyncTransforms();
                        var row = new EarlyMagicRow { Weapon = d.Type.ToString(), Name = d.Name, Stage = d.Stage, Damage = d.Damage,
                            Profile = "finite-native-enemy/" + ai, Target = "native-enemy/" + room.Item2 + "/" + template.Record,
                            ManaMax = mod.Stats.ManaMax, ManaCost = Equipment.ManaCost(d), Source = d.Source.ToString(),
                            Armor = string.Join("+", mod.Inventory.EffectiveArmor.Where(x => !x.IsEmpty).Select(x => x.Type)) };
                        // Count only the clone's accepted damage, including damage over time.
                        MeleeHits.DevAcceptedDamage = (h, damage) => { if (h == hm) { row.TotalDamage += damage; row.Hits++; } };
                        float start = Time.time, nextAim = 0, hurtAt = TerraHurt.LastHurtAt;
                        int slow0 = TerraMod.DevSlowCasts;
                        Vector2 aim = clone.transform.position;
                        yield return null;
                        if (hm == null) {
                            row.Profile += "/invalid-spawn";
                            row.Seconds = Time.time - start;
                            earlyMagicRows.Add(row); SaveEarlyMagic();
                            MeleeHits.DevAcceptedDamage = null; Object.Destroy(clone); EarlyMagicReset();
                            continue;
                        }
                        var info = EnemyStats.Of(hm);
                        row.EnemyLife = EnemyStats.LifeMax(info); row.Defense = info.Defense;
                        while (Time.time - start < 8f && hm != null && !hm.isDead && hm.hp > 0 && clone.activeInHierarchy)
                        {
                            // Stationary firing: the player does not dodge or chase. A failed kill is
                            // retained as a timeout, never converted into a successful clear.
                            hero.transform.position = stand; rb.linearVelocity = Vector2.zero;
                            if (Time.time >= nextAim) { nextAim = Time.time + .25f; aim = tc != null ? (Vector2)tc.bounds.center : (Vector2)clone.transform.position; if (d.Type == ItemType.CrimsonRod) aim += Vector2.up * 3; }
                            mod.DevAim = aim;
                            if (mod.DevReady && !mod.IsUsingItem && hero.CanInput() && (d.Type != ItemType.CrimsonRod || row.Casts == 0)) { mod.DevUseSelected(aim); row.Casts++; }
                            if (row.FirstDry < 0 && TerraMod.DevSlowCasts > slow0) row.FirstDry = Time.time - start;
                            if (TerraHurt.LastHurtAt != hurtAt) { hurtAt = TerraHurt.LastHurtAt; row.PlayerHits++; }
                            yield return null;
                        }
                        row.Killed = (hm == null || hm.isDead || hm.hp <= 0) && row.Hits > 0 && row.TotalDamage >= row.EnemyLife * .8f;
                        row.Seconds = Time.time - start; row.Dps = row.TotalDamage / Mathf.Max(.01f, row.Seconds);
                        row.ManaEnd = mod.Stats.Mana; row.SlowCasts = TerraMod.DevSlowCasts - slow0;
                        earlyMagicRows.Add(row); SaveEarlyMagic();
                        Plugin.Log.LogInfo($"[EARLY_MAGIC] {earlyMagicRows.Count} {row.Weapon} {row.Profile} {row.Target} killed={row.Killed} seconds={row.Seconds:F2} playerHits={row.PlayerHits}");
                        MeleeHits.DevAcceptedDamage = null;
                        Object.Destroy(clone);
                        EarlyMagicReset();
                        yield return new WaitForSeconds(.3f);
                    }
                }
                finally {
                    MeleeHits.DevAcceptedDamage = null;
                    foreach (var h in originals) if (h != null) h.gameObject.SetActive(true);
                    PlayerData.instance.isInvincible = true;
                }
                yield return GoTo("Town", "left1");
            }
        }
    }
}
