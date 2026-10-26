using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "nailknock"): the Nail's real swings at common enemies in the Forgotten Crossroads, how far each is
    /// thrown back with the Nail's old knockback (2) and its own now, beside one Copper Broadsword blow.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NailKnockTest()
        {
            if (!DevCopy.Verified) yield break;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            int pass = 0, all = 0;
            void Check(string name, bool ok, string detail = "")
            {
                all++; if (ok) pass++;
                Plugin.Log.LogInfo($"[AutoTest] 骨钉击退 {name} {(ok ? "通过" : "不通过")} {detail}");
            }
            var nail = ItemDb.Get(ItemType.KnightNail);
            float own = nail.Knockback;
            Check("骨钉的击退是阔剑的一般档", own >= 5f, $"{own}");
            foreach (var scene in new[] { "Crossroads_01", "Crossroads_07", "Crossroads_15" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                var kinds = new HashSet<string>();
                var targets = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => !h.isDead && h.gameObject.activeInHierarchy && EnemyStats.Of(h) is var i && i != null && !i.Boss && !i.Elite
                        && !TerraKnockback.Immune(h) && h.GetComponent<Rigidbody2D>()?.bodyType == RigidbodyType2D.Dynamic)
                    .Where(h => kinds.Add(EnemyStats.Of(h).Record)).Take(4).ToList();
                foreach (var hm in targets)
                {
                    string name = Bestiary.NameOf(EnemyStats.Of(hm).Record);
                    var results = new List<(string how, float back)>();
                    foreach (var how in new[] { "旧骨钉(2)", "骨钉", "铜阔剑" })
                    {
                        if (hm == null || hm.isDead) break;
                        hm.hp = 99999;
                        var col = hm.GetComponent<Collider2D>();
                        if (col == null) break;
                        // Beside it on its floor, a pace off.
                        float side = hero.transform.position.x < hm.transform.position.x ? -1f : 1f;
                        hero.transform.position = Clear(hero, new Vector2(hm.transform.position.x + side * 1.1f, col.bounds.min.y + 0.2f));
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        yield return new WaitForSeconds(1.3f);
                        if (hm == null || hm.isDead) break;
                        float away = Mathf.Sign(hm.transform.position.x - hero.transform.position.x);
                        float x0 = hm.transform.position.x, back = 0f;
                        if (how == "铜阔剑")
                        {
                            var sword = ItemDb.Get(ItemType.CopperBroadsword);
                            mod.Melee.HitWith(col, 5, Equipment.Knockback(sword), 0f, away, hero.GetComponent<BoxCollider2D>().bounds.center);
                        }
                        else
                        {
                            nail.Knockback = how == "骨钉" ? own : 2f;
                            InHand(ItemType.KnightNail);
                            pd.hasNailArt = false;
                            int hits0 = NailItem.DevHits;
                            // Turned to it and swinging until a slash lands (it may walk off a step meanwhile).
                            for (float t = 0f; t < 1.5f && NailItem.DevHits == hits0 && hm != null; t += Time.deltaTime)
                            {
                                if (hm.transform.position.x > hero.transform.position.x) hero.FaceRight(); else hero.FaceLeft();
                                NailAim.DevAim = col.bounds.center;
                                NailItem.DevHold = true;
                                x0 = hm.transform.position.x;
                                away = Mathf.Sign(hm.transform.position.x - hero.transform.position.x);
                                yield return null;
                            }
                            NailItem.DevHold = false;
                            NailAim.DevAim = null;
                            if (NailItem.DevHits == hits0) { results.Add((how, float.NaN)); continue; }
                        }
                        for (float t = 0f; t < 0.7f && hm != null; t += Time.deltaTime)
                        {
                            back = Mathf.Max(back, (hm.transform.position.x - x0) * away);
                            yield return null;
                        }
                        results.Add((how, back));
                    }
                    nail.Knockback = own;
                    string line = string.Join("，", results.Select(r => $"{r.how} {(float.IsNaN(r.back) ? "没砍中" : r.back.ToString("F2"))}"));
                    var old = results.FirstOrDefault(r => r.how == "旧骨钉(2)");
                    var now = results.FirstOrDefault(r => r.how == "骨钉");
                    var swd = results.FirstOrDefault(r => r.how == "铜阔剑");
                    bool ok = now.how != null && !float.IsNaN(now.back) && now.back > 0.3f && (swd.how == null || now.back >= swd.back * 0.7f);
                    Check($"{scene} {name} 骨钉打得退", ok, line);
                }
            }
            nail.Knockback = own;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = false;
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 骨钉击退 合计 {pass}/{all} {(pass == all ? "通过" : "不通过")}");
        }
    }
}
