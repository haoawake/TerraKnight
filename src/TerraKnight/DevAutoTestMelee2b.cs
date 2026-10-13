using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "melee2b", sweep S1.B): the thrown phaseblades (out, dropping, stuck, no swing
    /// while out, called back), their hits; the Keybrand on a hurt enemy; the Mandible Blade, the
    /// Stylish Scissors, the Waffle Iron and the Christmas Tree Sword's shots; pictures of a
    /// stuck blade, a rainbow one and the ornaments.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Saves Terraria textures as pictures (read back through a render texture), to check how a sprite faces.</summary>
        void DumpTextures(params string[] names)
        {
            foreach (var name in names)
            {
                var src = Terraria.TerrariaAssets.Texture(name);
                if (src == null) continue;
                var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, "tex_" + name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
        }

        /// <summary>A close-up round a world point (as CloseUp, which centres on the player).</summary>
        IEnumerator CloseUpAt(string scene, string name, Vector2 world, int halfPx = 90, int zoom = 3)
        {
            yield return new WaitForEndOfFrame();
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : Camera.main;
            if (cam == null) yield break;
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var sp = cam.WorldToScreenPoint(world);
            int cx = (int)sp.x, cy = (int)sp.y, w = halfPx * 2;
            var outTex = new Texture2D(w * zoom, w * zoom, TextureFormat.RGB24, false);
            var px = new Color[w * zoom * w * zoom];
            for (int y = 0; y < w * zoom; y++)
                for (int x = 0; x < w * zoom; x++)
                    px[y * w * zoom + x] = tex.GetPixel(Mathf.Clamp(cx - halfPx + x / zoom, 0, tex.width - 1), Mathf.Clamp(cy - halfPx + y / zoom, 0, tex.height - 1));
            outTex.SetPixels(px);
            outTex.Apply();
            string path = System.IO.Path.Combine(outDir, $"{scene}_{name}.png");
            System.IO.File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(outTex);
            Plugin.Log.LogInfo($"[AutoTest] 截图 {path}");
        }

        IEnumerator Melee2bTest(string scene, HeroController hero)
        {
            DumpTextures("Projectile_1059", "Projectile_1102", "Glow_373", "Projectile_1074", "Item_198", "Projectile_335", "Projectile_1082");
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            Wear();
            var tp = TerraProjectiles.Instance;
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            void Take(ItemType t)
            {
                inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(t);
                inv.Selected = Inventory.HotbarSize - 1;
            }
            IEnumerator Wait(float s) { for (float t = 0f; t < s; t += Time.deltaTime) yield return null; }

            // --- A throw with nothing to hit: out, dropping, stuck; no swing while out; called back.
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float dd = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (dd < best) { best = dd; target = hm; }
            }
            var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            void Park(Vector2 at)
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 3000);
                Pin(target, trb, at);
            }
            var away = C() - Vector2.right * side * 30f;
            foreach (var (item, kind, pic) in new[] { (ItemType.BluePhaseblade, ProjKind.PhaseBlue, "m2b_stuck_blue"), (ItemType.RainbowPhasesaber, ProjKind.SaberRainbow, "m2b_stuck_rainbow") })
            {
                tp.DevClear();
                Take(item);
                mod.DevHold = false;
                while (!mod.DevReady) yield return null;
                var aim = C() + new Vector2(side * 3f, -1.2f);
                mod.DevUseSelected(aim);
                int made = tp.DevCount(kind);
                float flying = -1f, stuckAt = -1f;
                for (float t = 0f; t < 2.5f; t += Time.deltaTime)
                {
                    Park(away);
                    float st = tp.DevState(kind);
                    if (flying < 0f && st == 0f) flying = t;
                    if (stuckAt < 0f && st == 2f) stuckAt = t;
                    yield return null;
                }
                var blade = tp.DevNewest(kind);
                if (blade.HasValue) yield return CloseUpAt(scene, pic, blade.Value, 60, 3);
                Plugin.Log.LogInfo($"[AutoTest] 近战2b 插住的剑在玩家 {(blade.HasValue ? ((blade.Value - C()) / Effects.TerraParticles.Px).ToString("F0") : "无")} px 处");
                // Used again while it is out: no swing, nothing new thrown.
                mod.DevUseSelected(aim);
                bool swung = mod.IsUsingItem;
                int still = tp.DevCount(kind);
                // The button held calls it back.
                mod.DevHold = true;
                float backIn = -1f;
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                {
                    Park(away);
                    if (tp.DevCount(kind) == 0) { backIn = t; break; }
                    yield return null;
                }
                mod.DevHold = false;
                Plugin.Log.LogInfo($"[AutoTest] 近战2b {ItemDb.Get(item).Name}：扔出 {made}，飞行中={flying >= 0f}，{stuckAt:F2} 秒后插住（原版约 0.5 秒起下落），插着时再用：挥动={swung}（False） 数量 {still}（1），按住后 {backIn:F2} 秒收回");
                yield return Wait(0.4f);
            }

            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 近战2b：场景里没有敌人，跳过敌人部分");
            }
            else
            {
                var near = C() + Vector2.right * side * 2.2f;
                var info = EnemyStats.Of(target);
                void Hold() { target.hp = Mathf.Max(target.hp, 3000); Pin(target, trb, near); }
                IEnumerator ShootFor(ItemType item, float seconds, int uses = 99)
                {
                    Take(item);
                    mod.DevHold = false;
                    int n = 0;
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        if (target == null || target.isDead) yield break;
                        Hold();
                        if (mod.DevReady && n < uses)
                        {
                            mod.DevUseSelected(near);
                            n++;
                        }
                        yield return null;
                    }
                }

                // A thrown blade at the enemy: the swing and the blade both hit.
                tp.DevClear();
                MeleeSpecials.DevHits.Clear();
                int hits0 = TerraProjectiles.DevHit(ProjKind.PhaseRed);
                yield return ShootFor(ItemType.RedPhaseblade, 1.6f, 1);
                Plugin.Log.LogInfo($"[AutoTest] 近战2b 红陨石光剑 打敌人：挥砍命中 {(MeleeSpecials.DevHits.TryGetValue(ItemType.RedPhaseblade, out int sw) ? sw : 0)}，飞剑命中 {TerraProjectiles.DevHit(ProjKind.PhaseRed) - hits0}，状态 {tp.DevState(ProjKind.PhaseRed)}");
                mod.DevHold = true;
                yield return Wait(1.2f);
                mod.DevHold = false;

                // The Keybrand: up to twice as hard on a badly hurt enemy.
                tp.DevClear();
                Take(ItemType.Keybrand);
                target.hp = Mathf.Max(2, Mathf.CeilToInt(info.MaxHp * 0.2f));
                Pin(target, trb, near);
                MeleeSpecials.DevHits.Clear();
                mod.DevUseSelected(near);
                for (float t = 0f; t < 0.6f && !MeleeSpecials.DevHits.ContainsKey(ItemType.Keybrand); t += Time.deltaTime) { Pin(target, trb, near); yield return null; }
                yield return CloseUp(scene, "m2b_keybrand", hero, 120, 2);
                float lifeFrac = (float)EnemyStats.Life(target, info) / EnemyStats.LifeMax(info);
                Plugin.Log.LogInfo($"[AutoTest] 近战2b 钥匙剑：命中={MeleeSpecials.DevHits.ContainsKey(ItemType.Keybrand)}，{MeleeSpecials.DevLastBase}→{MeleeSpecials.DevLastModified}（约 20% 血时原版 ×1.89）");
                mod.DevHold = true;
                yield return Wait(1.5f);
                mod.DevHold = false;

                // The shooting swords.
                foreach (var (item, kind, label) in new[]
                {
                    (ItemType.AntlionClaw, ProjKind.MandibleBlade, "颌骨剑"),
                    (ItemType.StylistKilLaKillScissorsIWish, ProjKind.StylishScissors, "时尚剪刀"),
                    (ItemType.WaffleIron, ProjKind.Waffle, "华夫饼烘烤模"),
                    (ItemType.ChristmasTreeSword, ProjKind.ChristmasOrnament, "圣诞树剑"),
                })
                {
                    tp.DevClear();
                    EnemyDebuffs.Clear();
                    int m0 = TerraProjectiles.DevMade(kind), h0 = TerraProjectiles.DevHit(kind), o0 = TerraProjectiles.DevMade(ProjKind.Ornament);
                    yield return ShootFor(item, 1.5f);
                    if (kind == ProjKind.ChristmasOrnament) yield return CloseUp(scene, "m2b_ornaments", hero, 160, 2);
                    Plugin.Log.LogInfo($"[AutoTest] 近战2b {label}：射出 {TerraProjectiles.DevMade(kind) - m0}，命中 {TerraProjectiles.DevHit(kind) - h0}，小装饰 {TerraProjectiles.DevMade(ProjKind.Ornament) - o0}，" +
                                       $"流血={EnemyDebuffs.Has(target, EnemyDebuffs.Bleeding)} 狱火={EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire)}");
                }
            }

            mod.DevHold = null;
            tp.DevClear();
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
