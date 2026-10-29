using System;
using System.Collections;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        // Count actual visible humanoid sprites, including separately parented outline/ghost copies.
        static int VisibleRider(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent)
                if (child.name.StartsWith("Terra Player"))
                    count += child.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy && r.sprite != null && r.color.a > .01f);
            return count;
        }

        IEnumerator MountVisibilityTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int checks = 0, failures = 0, oldStage = Shop.DevStage;
            bool freeBefore=mod.Inventory.FreeMaking;
            void Check(string name, bool pass)
            {
                checks++; if (!pass) failures++;
                Plugin.Log.LogInfo("[AutoTest] 变身外观/" + name + (pass ? " 通过" : " 不通过"));
            }
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false); mod.Inventory.Infinite = false; Shop.DevStage = 5;
            // This suite checks rendering, not boss gates: make every tested mount available in its private save.
            mod.Inventory.FreeMaking=true;
            PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            Equip(Array.Empty<ItemType>(), Array.Empty<ItemType>());
            var hero = HeroController.instance; var rb = hero.GetComponent<Rigidbody2D>();
            var start = Clear(hero, new Vector2(110f, 13f));
            var remote = new RemotePlayer { Slot = 237, Name = "Visibility test" };
            try
            {
                foreach (int id in new[] { 55, 52, 56, 61, 54, 1, 57 })
                {
                    Mounts.Dismount(); hero.transform.position = start; rb.position = start; rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(.15f);
                    Check("解除后人物恢复/" + id, VisibleRider(mod.transform) > 0);
                    Check("实际骑乘/" + id, Mounts.TryMount(Mounts.Get(id), hero));
                    yield return new WaitForSeconds(.15f);
                    // No helmet equipped: all five transformation types must show zero humanoid layers.
                    bool transformed = id == 52 || id == 54 || id == 55 || id == 56 || id == 61;
                    Check("本地连续帧无残留/" + id, transformed ? VisibleRider(mod.transform) == 0 : VisibleRider(mod.transform) > 0);
                    yield return new WaitForEndOfFrame();
                    RenderMountPhoto(id, "_local");
                    var state = new PlayerState { Scene = "Town", Feet = NetScenes.ToLocal("Town", start), Shown = true,
                        FacingRight = true, Grounded = true, Mount = id, Opacity = 1f, ItemScale = 1f, Swing = -1f,
                        Body = -1, Leg = -1, Shadows = 3, Eq = new TerraPlayerSprite.Equips { Stealth = 1f },
                        Wing = 1, WingAir = true };
                    // Exercise the real serialization and receiving renderer, rather than a duplicate visibility calculation.
                    using (var memory = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true)) state.Write(writer);
                        memory.Position = 0; state = PlayerState.Read(new BinaryReader(memory));
                    }
                    remote.Receive(state); remote.Pose();
                    var root = GameObject.Find("Terra Net Player 237");
                    Check("远端人物分层/" + id, root != null && (transformed ? VisibleRider(root.transform) == 0 : VisibleRider(root.transform) > 0));
                    // Repeated frames must not recreate player outlines or armor afterimages under a transformation.
                    for (int frame = 0; frame < 3; frame++) { yield return new WaitForEndOfFrame(); remote.Pose(); }
                    Check("远端连续帧无残留/" + id, root != null && (transformed ? VisibleRider(root.transform) == 0 : VisibleRider(root.transform) > 0));
                    RenderMountPhoto(id, "_remote");
                    state.Mount = -1; state.Wing = 0; state.Shadows = 0;
                    remote.Receive(state); remote.Pose();
                    Check("远端解除后恢复/" + id, root != null && VisibleRider(root.transform) > 0);
                    remote.Destroy();
                }
                // Raptor preserves actual headgear while hiding the player's naked head, torso, arms and legs.
                hero.transform.position = start; rb.position = start; rb.linearVelocity = Vector2.zero;
                Mounts.Dismount();
                Equip(new[] { ItemType.CopperHelmet, ItemType.CopperChainmail, ItemType.CopperGreaves }, Array.Empty<ItemType>());
                yield return new WaitForSeconds(.2f);
                Check("迅猛龙装备后骑乘", Mounts.TryMount(Mounts.Get(54), hero));
                yield return new WaitForSeconds(.2f);
                Check("迅猛龙只保留头盔", VisibleRider(mod.transform) > 0 && VisibleRider(mod.transform) <= 2);
                yield return new WaitForEndOfFrame(); RenderMountPhoto(54, "_helmet");
                foreach (int direction in new[] { 1, -1 })
                {
                    // DevMove injects velocity directly; a real direction key also turns the hero.
                    if(direction>0)hero.FaceRight();else hero.FaceLeft();
                    TerraMovement.DevMove = direction;
                    yield return new WaitForSeconds(.25f);
                    var rider = mod.GetComponentsInChildren<TerraPlayerSprite>(true).First(p => p.transform.parent == mod.transform);
                    var visibleHead = rider.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r => r.enabled && r.sprite != null && r.color.a > .01f);
                    Check("奔跑头盔跟随头部/" + direction, visibleHead != null && (visibleHead.bounds.center.x - hero.transform.position.x) * direction > .2f);
                    yield return new WaitForEndOfFrame(); RenderMountPhoto(54, "_running_" + direction);
                }
                TerraMovement.DevMove = 0;
                Mounts.Dismount(); yield return new WaitForSeconds(.2f);
                Check("盔甲及身体完整恢复", VisibleRider(mod.transform) > 5);
                yield return new WaitForEndOfFrame(); RenderMountPhoto(54, "_dismounted");
            }
            finally { remote.Destroy(); Mounts.Dismount(); TerraMovement.DevMove = 0; Shop.DevStage = oldStage; mod.Inventory.FreeMaking=freeBefore; }
            Plugin.Log.LogInfo($"[AutoTest] 变身外观 合计 {checks - failures}/{checks} 通过");
        }
    }
}
