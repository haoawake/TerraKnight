using System.Collections;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "terrarecoil"): a hard thing beside the player (the game's own TinkEffect, as on
    /// spikes and armour), struck with a Terraria broadsword: no recoil off it (Terraria's weapons
    /// never push the player back); then with the Nail item, the Knight's own nail: the game's
    /// recoil as ever.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TerraRecoilTest()
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(0.5f);
            // A layer the slashes (Hero Attack, 17) touch.
            int layer = 11;
            foreach (int l in new[] { 11, 12, 13, 8, 0 })
                if (!Physics2D.GetIgnoreLayerCollision(17, l)) { layer = l; break; }
            var effect = new GameObject("Test Tink Effect");
            effect.SetActive(false);
            int ok = 0, all = 0;
            foreach (bool nail in new[] { false, true })
            {
                hero.FaceRight();
                Vector2 at = col.bounds.center;
                var hard = new GameObject("Test Tink") { layer = layer };
                hard.transform.position = new Vector3(at.x + 1.4f, at.y, 0f);
                var box = hard.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.8f, 2.4f);
                box.isTrigger = true;
                var tink = hard.AddComponent<TinkEffect>();
                tink.blockEffect = effect;
                var inv = mod.Inventory;
                inv.Slots[0] = ItemStack.Of(nail ? ItemType.KnightNail : ItemType.IronBroadsword);
                inv.Selected = 0;
                yield return new WaitForSeconds(0.4f);
                int tinks0 = PropHits.DevTinksStopped, recoils0 = NailAttackProxy.DevRecoilsStopped, swings0 = NailItem.DevSwings;
                float x0 = hero.transform.position.x, minVx = 0f;
                bool recoiled = false;
                Vector2 aim = hard.transform.position;
                if (nail) NailItem.DevHold = true;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    if (!nail)
                    {
                        mod.DevAim = aim;
                        if (t < 0.1f && mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                    }
                    else if (t > 0.25f) NailItem.DevHold = false;
                    yield return null;
                    recoiled |= hero.cState.recoilingLeft;
                    minVx = Mathf.Min(minVx, rb.linearVelocity.x);
                }
                NailItem.DevHold = null;
                mod.DevAim = null;
                float moved = hero.transform.position.x - x0;
                int tinks = PropHits.DevTinksStopped - tinks0, stopped = NailAttackProxy.DevRecoilsStopped - recoils0;
                all++;
                bool pass = nail ? recoiled : !recoiled && tinks + stopped > 0;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 骨钉后退 {(nail ? $"骨钉（挥了 {NailItem.DevSwings - swings0} 下）" : "铁阔剑")} 砍原版的硬东西（层 {layer}）：{(recoiled ? "被弹开了" : "没被弹开")}，" +
                                   $"往后最快 {minVx:F1}，挪了 {moved:F2}；拦下的叮 {tinks} 次、后退 {stopped} 次{(nail ? "（原版骨钉应照常后退）" : "")} {(pass ? "通过" : "不通过")}");
                Object.Destroy(hard);
                yield return new WaitForSeconds(0.6f);
            }
            Object.Destroy(effect);
            Plugin.Log.LogInfo($"[AutoTest] 骨钉后退 合计 {ok}/{all}");
        }
    }
}
