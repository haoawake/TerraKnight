using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shieldbreak", the user's 10-29 "射手完全打不了带盾的敌人，当盾受到1倍敌人血量的伤害后能够暂时碎裂"): a Husk
    /// Warrior in the Crossroads with its shield up towards the player and held there; arrows into the shield are stopped
    /// until they add up to its life, then it breaks ("破盾！"), an arrow into it lands, and after ShieldBreak.BrokenFor it
    /// stops them again. Lines "[AutoTest] ShieldBreak …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShieldBreakTest()
        {
            int pass = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            { total++; if (ok) pass++; Plugin.Log.LogInfo($"[AutoTest] ShieldBreak {name}：{detail} {(ok ? "通过" : "不通过")}"); }
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WoodenArrow, 999);
            Player.TerraStats.DevImmortal = true;
            yield return GoTo("Crossroads_15", EntryGateOf("Crossroads_15"));
            PlayerData.instance.isInvincible = true;
            var husks = EnemiesNamed("Zombie Shield");
            if (husks.Count == 0) { Check("躯壳战士", false, "Crossroads_15 没有"); yield break; }
            var hm = husks[0];
            var info = EnemyStats.Of(hm);
            PlayMakerFSM control = null;
            foreach (var f in hm.GetComponents<PlayMakerFSM>()) if (f.FsmName == "ZombieShieldControl") control = f;
            var c = (Vector2)hm.GetComponent<Collider2D>().bounds.center;
            bool raised = false;
            for (int k = 0; k < 6 && !raised; k++)
            {
                c = hm.GetComponent<Collider2D>().bounds.center;
                hero.transform.position = new Vector3(c.x - 2.2f, c.y + 0.2f, hero.transform.position.z);
                hero.FaceRight();
                float wait = Time.time + 2.5f;
                while (Time.time < wait && !(hm.IsInvincible && hm.InvincibleFromDirection != 0 && StateStarts(hm, "ZombieShieldControl", "Shield Left"))) { PlayerData.instance.isInvincible = true; yield return null; }
                raised = hm.IsInvincible && StateStarts(hm, "ZombieShieldControl", "Shield Left");
            }
            Check("举盾", raised && control != null, Guard(hm));
            if (!raised || control == null) yield break;
            control.enabled = false;
            var body = hm.GetComponent<Rigidbody2D>();
            if (body != null) { body.linearVelocity = Vector2.zero; body.isKinematic = true; }
            var hold = (Vector2)hm.transform.position;
            c = hm.GetComponent<Collider2D>().bounds.center;
            float life = EnemyStats.LifeMax(info);
            IEnumerator Shoot()
            {
                hm.transform.position = new Vector3(hold.x, hold.y, hm.transform.position.z);
                hero.transform.position = new Vector3(c.x - 4f, c.y + 0.2f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                PlayerData.instance.isInvincible = true;
                yield return UseOn(ItemType.Tsunami, c);
                yield return new WaitForSeconds(0.45f);
            }
            int broken0 = ShieldBreak.DevBroken, blocked0 = MeleeHits.DevBlocked, hp0 = hm.hp, shots = 0;
            float until = Time.time + 60f;
            while (ShieldBreak.DevBroken == broken0 && Time.time < until)
            {
                shots++;
                yield return Shoot();
                if (shots % 10 == 0) Plugin.Log.LogInfo($"[AutoTest] ShieldBreak 射 {shots} 次，盾已挡 {ShieldBreak.DevTaken(hm):F0}/{life:F0}，上一下 {MeleeHits.LastStruck}");
            }
            Check("挡下的伤害够一条命后碎盾", ShieldBreak.DevBroken > broken0 && hm.hp == hp0,
                $"射 {shots} 箭，挡 {MeleeHits.DevBlocked - blocked0} 下，命 {life:F0}，掉血 {hp0 - hm.hp}");
            Shot("Crossroads_15", "shieldbreak_broken");
            // A hit too small for a point of the enemy's own health goes towards the next (its carry): landed all the same.
            int before = hm.hp, blockedBroken = MeleeHits.DevBlocked;
            float carry = info.Carry;
            yield return Shoot();
            yield return new WaitForSeconds(0.3f);
            Check("碎盾后箭能打中", (hm.hp < before || info.Carry != carry) && MeleeHits.DevBlocked == blockedBroken && ShieldBreak.Broken(hm),
                $"掉血 {before - hm.hp}，累计 {carry:F2}->{info.Carry:F2}，挡 {MeleeHits.DevBlocked - blockedBroken}，仍碎 {ShieldBreak.Broken(hm)}");
            yield return new WaitForSeconds(ShieldBreak.BrokenFor);
            int blocked1 = MeleeHits.DevBlocked;
            before = hm.hp;
            yield return Shoot();
            yield return new WaitForSeconds(0.3f);
            Check($"{ShieldBreak.BrokenFor:F0} 秒后盾恢复", !ShieldBreak.Broken(hm) && hm.hp == before && MeleeHits.DevBlocked > blocked1, $"掉血 {before - hm.hp}，挡 {MeleeHits.DevBlocked - blocked1}");
            control.enabled = true;
            if (body != null) body.isKinematic = false;
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            Plugin.Log.LogInfo($"[AutoTest] ShieldBreak 合计 {pass}/{total}");
        }
    }
}
