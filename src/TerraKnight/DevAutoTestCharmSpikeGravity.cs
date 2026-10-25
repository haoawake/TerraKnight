using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator CharmSpikeGravityTest()
        {
            if (!DevCopy.Verified) yield break;
            yield return CharmPlace();
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            var body = hero.GetComponent<Rigidbody2D>();
            WearOnly(37, 14);
            mod.Inventory.Accessories[0] = ItemStack.Of(ItemType.HermesBoots);
            Equipment.Update(mod.Inventory);
            pd.isInvincible = false;
            TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            int pass = 0, all = 0;
            void Check(string name, bool ok, string detail)
            {
                all++; if (ok) pass++;
                Plugin.Log.LogInfo($"[AutoTest] 刺伤重力 {name} {(ok ? "通过" : "不通过")} {detail}");
            }
            var safe = hero.transform.position;
            try
            {
                foreach (bool dash in new[] { false, true })
                {
                    hero.transform.position = safe;
                    body.gravityScale = hero.DEFAULT_GRAVITY;
                    body.linearVelocity = Vector2.zero;
                    hero.SetHazardRespawn(safe, true);
                    mod.Stats.Life = mod.Stats.LifeMax;
                    for (float until = Time.time + 5f; (hero.cState.invulnerable || !CanBeHit(hero)) && Time.time < until;) yield return null;
                    int life = mod.Stats.Life;
                    if (dash) { CharmEffects.Press(hero, 14); TerraMovement.HkShadowDash(hero, 1); }
                    var source = new GameObject("Spike gravity regression");
                    hero.TakeDamage(source, GlobalEnums.CollisionSide.bottom, 1, 2);
                    Object.Destroy(source);
                    Check(dash ? "冲刺中刺伤实际扣血" : "佩戴组合刺伤实际扣血", mod.Stats.Life < life, $"life={life}->{mod.Stats.Life}, gravity={body.gravityScale}");
                    yield return new WaitForSeconds(.3f);
                    for (float until = Time.time + 9f; (hero.cState.hazardDeath || hero.cState.hazardRespawning || !GameHooks.PlayerHasControl()) && Time.time < until;) yield return null;
                    yield return new WaitForSeconds(.4f);
                    Check(dash ? "冲刺刺伤回场恢复重力" : "刺伤回场恢复重力", body.gravityScale > .01f && GameHooks.PlayerHasControl(), $"gravity={body.gravityScale}, input={hero.acceptingInput}, hazard={hero.cState.hazardDeath}/{hero.cState.hazardRespawning}");
                    hero.transform.position = Clear(hero, safe + Vector3.up * 2f);
                    body.linearVelocity = Vector2.zero;
                    hero.cState.onGround = false;
                    float before = hero.transform.position.y;
                    yield return new WaitForSeconds(.35f);
                    Check(dash ? "冲刺刺伤后空中正常下落" : "刺伤后空中正常下落", hero.transform.position.y < before - .1f, $"y={before}->{hero.transform.position.y}, gravity={body.gravityScale}");
                }
                hero.RelinquishControl();
                hero.AffectedByGravity(false);
                yield return new WaitForSeconds(.3f);
                Check("剧情接管期间不强开重力", Mathf.Abs(body.gravityScale) < .01f, $"gravity={body.gravityScale}");
            }
            finally
            {
                hero.RegainControl(); hero.AffectedByGravity(true);
                body.gravityScale = hero.DEFAULT_GRAVITY;
                WearOnly(); Charms.DevAnywhere = false;
                TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 刺伤重力结果 {pass}/{all}");
        }
    }
}
