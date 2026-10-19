using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_bduels_zh"): recorder B's class duels for the 4.0 trailer: each class with its best weapons
    /// against a boss of its own, reached by the boss's summon (straight into the arena, the fight begun), recorded
    /// from the first blow, the hero kept alive, the view widened so hero and boss both fit.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>A boss summon used from the hand (its first kill counted, the stage open): into its arena, the fight on, control back.</summary>
        IEnumerator BSummonBoss(string key)
        {
            BossSummons.Seal seal = null;
            foreach (var s in BossSummons.All) if (s.Key == key) seal = s;
            if (seal == null) { Plugin.Log.LogInfo($"[AutoTest] 录制B 没有召唤物 {key}"); yield break; }
            var inv = mod.Inventory;
            Bestiary.Kills[key] = Mathf.Max(1, Bestiary.Kills.TryGetValue(key, out int k) ? k : 0);
            Shop.DevStage = 5;
            int slot = -1;
            for (int i = 0; i < Inventory.Size; i++) if (inv.Slots[i].IsEmpty) { slot = i; break; }
            if (slot < 0) slot = Inventory.Size - 1;
            inv.Slots[slot] = ItemStack.Of(seal.Type, 2);
            inv.Selected = slot;
            inv.Override = -1;
            for (float t = Time.realtimeSinceStartup + 10f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
            string why = BossSummons.Refusal(seal);
            if (why != null) { Plugin.Log.LogInfo($"[AutoTest] 录制B 召唤物 {key} 不能用：{why}"); yield break; }
            int started = BossSummonStart.DevStarted;
            mod.DevUseSelected(HeroController.instance.transform.position);
            for (float t = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(key) && Time.realtimeSinceStartup < t;) { BKeep(); yield return null; }
            string scene = GameManager.instance.sceneName;
            bool dream = scene.StartsWith("GG_") || scene.StartsWith("Dream_") || scene.StartsWith("Grimm_Nightmare");
            for (float t = Time.realtimeSinceStartup + 20f; !dream && BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < t;) { BKeep(); yield return null; }
            for (float t = Time.realtimeSinceStartup + 12f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) { BKeep(); yield return null; }
            inv.Slots[slot] = default;
            Plugin.Log.LogInfo($"[AutoTest] 录制B 召唤物 {key}：在 {scene}，到达 {BossArena.Arrived(key)}，开战 {BossSummonStart.DevLast}，可操作 {GameHooks.PlayerHasControl()}");
        }

        /// <summary>
        /// A class against a boss: its summon used, minions out first for the summoner, then its weapons in turn (a slice of
        /// the fight each), aimed at the nearest part of the boss, the hero on wings at a distance on one side, over to the
        /// other now and then; recorded until `seconds` or a moment after the boss falls.
        /// </summary>
        IEnumerator BDuel(string cls, int loadout, string key, ItemType[] weapons, float seconds, float dist, ItemType[] summons = null, float zoom = 1.5f)
        {
            mod.TrySwitchLoadout(loadout);
            yield return null;
            BWear(ItemType.EmpressFlightBooster);
            Minions.Instance?.Clear();
            Player.TerraCamera.Zoom = 2f;
            yield return BSummonBoss(key);
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            string record = key.Split('@')[0];
            // Up to see the boss first (up to a few seconds).
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                BKeep();
                bool up = false;
                foreach (var hm in BossesOf(record, false)) if (!hm.isDead && hm.gameObject.activeInHierarchy) up = true;
                if (up) break;
                yield return null;
            }
            HealthManager Nearest()
            {
                HealthManager best = null;
                float bd = float.MaxValue;
                foreach (var hm in BossesOf(record, false))
                {
                    if (hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                    float d = Vector2.Distance(BCentre(hm), col.bounds.center);
                    if (d < bd) { bd = d; best = hm; }
                }
                return best;
            }
            var first = Nearest();
            aimInit = false;
            if (first != null) SmoothAim(BCentre(first), 99f);
            bSide = first != null && col.bounds.center.x < BCentre(first).x ? -1 : 1;
            bSwapAt = Time.time + 4f;
            bRow = null;
            string clip = "duel_" + cls;
            StartRec(clip);
            float start = Time.time;
            // The view eased out as the fight opens.
            if (summons != null)
                foreach (var s in summons)
                {
                    int si = SlotOf(s);
                    if (si < 0) continue;
                    inv.Selected = si;
                    for (int n = 0; n < (s == ItemType.StardustDragonStaff ? 3 : 2); n++)
                    {
                        var spot = (Vector2)col.bounds.center + new Vector2(n % 2 == 0 ? 1.5f : -1.5f, 1.8f);
                        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
                        {
                            BKeep();
                            Player.TerraCamera.Zoom = Mathf.Lerp(2f, zoom, Mathf.Clamp01((Time.time - start) / 1.5f));
                            SmoothAim(spot, 14f);
                            var b = Nearest();
                            if (b != null) BSteer(hero, BCentre(b), dist, false);
                            yield return null;
                        }
                        while (!mod.DevReady || mod.IsUsingItem) { BKeep(); yield return null; }
                        mod.DevUseSelected(aimNow);
                    }
                }
            float quiet = 0f;
            float slice = seconds / weapons.Length;
            int wi = -1;
            var marks = new System.Text.StringBuilder();
            while (Time.time - start < seconds && quiet < 1.8f)
            {
                BKeep();
                Player.TerraCamera.Zoom = Mathf.Lerp(2f, zoom, Mathf.Clamp01((Time.time - start) / 1.5f));
                var target = Nearest();
                quiet = target == null ? quiet + Time.deltaTime : 0f;
                int want = Mathf.Min(weapons.Length - 1, (int)((Time.time - start) / slice));
                if (want != wi)
                {
                    // A weapon change waits for the one out to come back (a yoyo, a flail).
                    mod.DevHold = null;
                    if (!mod.IsUsingItem)
                    {
                        int si = SlotOf(weapons[want]);
                        if (si >= 0) inv.Selected = si;
                        wi = want;
                        marks.Append($"{ItemDb.Get(weapons[want]).Name}@{Time.time - start:F1} ");
                    }
                }
                if (target != null)
                {
                    Vector2 at = BCentre(target);
                    SmoothAim(at, 10f);
                    if (wi == want)
                    {
                        mod.DevHold = true;
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow);
                    }
                    BSteer(hero, at, dist);
                }
                else
                {
                    mod.DevHold = false;
                    Player.TerraMovement.DevMove = 0;
                    Player.TerraMovement.DevJump = false;
                }
                yield return null;
            }
            BStop();
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { BKeep(); yield return null; }
            StopRec();
            mod.DevAim = null;
            Player.TerraCamera.Zoom = 2f;
            Plugin.Log.LogInfo($"[AutoTest] 录制B 片段 {clip}：{record} {(quiet >= 1.8f ? "打倒了" : "没打完")}，{Time.time - start:F1}s，武器 {marks}");
            // Out of the arena (and any dream) for the next.
            for (float t = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) { BKeep(); yield return null; }
            yield return EnterRoom("Town");
            for (float t = Time.realtimeSinceStartup + 10f; (BossArena.Visiting || BossSummons.Pending) && Time.realtimeSinceStartup < t;) yield return null;
        }

        IEnumerator P4_bduels(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            yield return EnterRoom("Town");
            yield return BDuel("melee", 0, "Hornet@4", new[] { ItemType.Zenith, ItemType.StarWrath, ItemType.DayBreak }, 15f, 4f);
            yield return BDuel("ranged", 1, "HollowKnightPrime", new[] { ItemType.SDMG, ItemType.Phantasm, ItemType.CelebrationMk2 }, 15f, 7f);
            yield return BDuel("magic", 2, "MageLord@5", new[] { ItemType.LastPrism, ItemType.LunarFlareBook, ItemType.NebulaBlaze }, 15f, 6.5f);
            yield return BDuel("summon", 3, "Grimm", new[] { ItemType.Kaleidoscope, ItemType.MoonLordWhip }, 15f, 4.5f, new[] { ItemType.StardustDragonStaff, ItemType.Terraprisma });
        }

        IEnumerator P4_bduel_summon(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            yield return EnterRoom("Town");
            yield return BDuel("summon", 3, "Grimm", new[] { ItemType.Kaleidoscope, ItemType.MorningStar }, 15f, 4.5f, new[] { ItemType.StardustDragonStaff, ItemType.Terraprisma });
        }

        IEnumerator P4_bnext(HeroController hero)
        {
            yield return P4_bduel_summon(hero);
            yield return P4_bmove(hero);
        }
    }
}
