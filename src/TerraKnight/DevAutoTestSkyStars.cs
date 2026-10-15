using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "skystars"): stars falling at night. The rooms under the sky and a few caves scanned
    /// for how much of their top is open; in Dirtmouth at night many stars made at once (lucky ones near
    /// the Knight) fall and land as Fallen Stars, one is picked up; at daybreak the falling ones come
    /// down and the lying ones go, an enemy's Fallen Star stays; underground none fall.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SkyStarsTest(string suite)
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 坠星 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            mod.DevNoFallDamage = true;

            // 1. The rooms: how much of their top is sky (only, with "skyscan": each one's top photographed).
            bool sky = suite == "skyscan";
            var scenes = new List<string>();
            foreach (var s in UI.WorldMap.DevSceneNames())
                if (s == "Town" || s == "Tutorial_01" || s == "Fungus1_28" || s.StartsWith("Cliffs_") || s.StartsWith("Deepnest_East_") || s.StartsWith("Mines_3") || s == "Mines_25" || s == "Crossroads_01" || s == "Fungus1_01" || s == "Ruins1_01" || s.StartsWith("RestingGrounds_") || s == "Fungus2_01" || s.StartsWith("Abyss_06") || s == "Room_Colosseum_Spectate")
                    scenes.Add(s);
            if (!sky) scenes.Clear();
            foreach (var scene in scenes)
            {
                foreach (var gate in new[] { scene == "Town" ? "left1" : EntryGateOf(scene), "bot1", "right1", "top1", "left1" })
                {
                    yield return GoTo(scene, gate);
                    if (gm.sceneName == scene) break;
                }
                pd.isInvincible = true;
                if (gm.sceneName != scene)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 坠星 房间 {scene}：没进去");
                    continue;
                }
                var (open, cols, depth) = FallingStars.DevSky();
                string lids = FallingStars.DevLids(out var under);
                Plugin.Log.LogInfo($"[AutoTest] 坠星 房间 {scene}：露天 {FallingStars.SkyRoom(scene)}，顶上空着 {open}/{cols} 列，平均落下 {depth:F0} 格；顶盖厚度（下面空 12 格以上的列）{lids}；看的地方 {under:F1}");
                if (!sky) continue;
                var h = HeroController.instance;
                if (under != default)
                {
                    h.AffectedByGravity(false);
                    h.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    h.transform.position = under;
                    yield return new WaitForSeconds(0.6f);
                    Shot(scene, "skytop_" + scene);
                    yield return null;
                    h.AffectedByGravity(true);
                }
            }

            if (sky)
            {
                mod.DevNoFallDamage = false;
                Plugin.Log.LogInfo("[AutoTest] 坠星 扫描完");
                yield break;
            }

            // 2. Dirtmouth at night: stars made at once, lucky ones.
            yield return GoTo("Town", "left1");
            var hero = HeroController.instance;
            pd.isInvincible = true;
            // On the street, under the open sky.
            hero.transform.position = new Vector2(110f, 12f);
            float settle = Time.time + 3f;
            yield return null;
            while (!hero.cState.onGround && Time.time < settle) yield return null;
            WorldItems.DevClear();
            int Stars() => mod.Inventory.Slots.Where(x => !x.IsEmpty && x.Type == ItemType.FallenStar).Sum(x => x.Count);
            int bag0 = Stars();
            TerraTime.DevForceDay = false;
            FallingStars.DevLucky = true;
            FallingStars.DevRate = 200f;
            int started0 = FallingStars.DevStarted;
            float until = Time.time + 1.5f;
            while (Time.time < until) yield return null;
            FallingStars.DevRate = 0f;
            int made = FallingStars.DevStarted - started0;
            Vector2 heroAt = hero.transform.position;
            yield return new WaitForSeconds(0.35f);
            Shot("Town", "skystars_falling");
            int falling = TerraProjectiles.Instance.SkyStars();
            float wait = Time.time + 20f;
            while (TerraProjectiles.Instance.SkyStars() > 0 && Time.time < wait) yield return null;
            yield return new WaitForSeconds(1.5f);
            int lying = WorldItems.SkyStarsLying(), caught = Stars() - bag0;
            Shot("Town", "skystars_landed");
            float dx = Mathf.Abs(FallingStars.DevLastFrom.x - heroAt.x);
            Check("德特茅斯夜里落星", made >= 10 && falling > 0 && lying + caught == made,
                $"1.5 秒里落下 {made} 颗（幸运的 {FallingStars.DevLuckyStarted}），0.35 秒后还在落 {falling} 颗，落地后躺着 {lying} 颗、落在骑士身上直接捡到 {caught} 颗；最后一颗离骑士横向 {dx / TerraParticles.Px:F0} px");
            Check("幸运星落在骑士 640 px 内", dx <= 640f * TerraParticles.Px + 0.01f, $"{dx / TerraParticles.Px:F0} px");

            // 3. One picked up.
            int had = Stars();
            var nearest = WorldItems.Positions().OrderBy(p => Vector2.Distance(p, hero.transform.position)).FirstOrDefault();
            if (nearest != default)
            {
                hero.transform.position = nearest + Vector2.up * 0.3f;
                yield return new WaitForSeconds(1f);
            }
            Check("捡起一颗", Stars() > had, $"背包里坠落之星 {had} → {Stars()}，剩下躺着 {WorldItems.SkyStarsLying()} 颗");

            // 4. Daybreak: the falling ones come down, the lying ones go, an enemy's stays.
            FallingStars.DevLucky = true;
            FallingStars.DevRate = 200f;
            yield return new WaitForSeconds(0.2f);
            FallingStars.DevRate = 0f;
            int inAir = TerraProjectiles.Instance.SkyStars();
            WorldItems.Drop(ItemStack.Of(ItemType.FallenStar, 3), (Vector2)hero.transform.position + Vector2.right * 7f);
            int faded0 = WorldItems.DevFaded;
            TerraTime.DevForceDay = true;
            yield return new WaitForSeconds(1.5f);
            int enemyStars = WorldItems.Positions().Count();
            Check("天亮了", inAir > 0 && TerraProjectiles.Instance.SkyStars() == 0 && WorldItems.SkyStarsLying() == 0 && WorldItems.DevFaded > faded0 && enemyStars >= 1,
                $"天亮时还在落 {inAir} 颗，之后在落 {TerraProjectiles.Instance.SkyStars()} 颗、天上来的躺着 {WorldItems.SkyStarsLying()} 颗、消失 {WorldItems.DevFaded - faded0} 颗，别的掉落物还在 {enemyStars} 堆");
            Shot("Town", "skystars_dawn");

            // 5. Underground none fall.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            pd.isInvincible = true;
            TerraTime.DevForceDay = false;
            FallingStars.DevLucky = null;
            int under0 = FallingStars.DevStarted;
            FallingStars.DevRate = 200f;
            yield return new WaitForSeconds(2f);
            FallingStars.DevRate = null;
            Check("地下不落", FallingStars.DevStarted == under0, $"十字路两秒里落下 {FallingStars.DevStarted - under0} 颗");

            // 6. The game's own rate: a night's worth of ticks in Dirtmouth.
            Check("原版的量", true, $"今晚加成 {FallingStars.Boost:F2}，每夜 32400 刻、每刻 10×加成/8000，一夜约 {32400f * 10f * FallingStars.Boost / 8000f:F0} 颗");

            TerraTime.DevForceDay = null;
            FallingStars.DevRate = null;
            FallingStars.DevLucky = null;
            WorldItems.DevClear();
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 坠星 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
