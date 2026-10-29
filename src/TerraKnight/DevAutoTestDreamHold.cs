using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dreamhold", the user's 10-28 "拿梦钉的时候在做俯卧撑"): the Dream Nail held in Dirtmouth for three seconds
    /// and let go, recorded as animreal records (a shot every other frame), each frame's hero clip, cutscene pose, Terraria
    /// body and leg frames and the Knight's height written down. Lines "[AutoTest] 梦钉握持 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DreamHoldTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            pd.hasDreamNail = true;
            int nail = SlotOf(Items.ItemType.DreamNail);
            if (nail < 0) { mod.Inventory.Slots[0] = Items.ItemStack.Of(Items.ItemType.DreamNail); nail = 0; }
            mod.Inventory.Selected = nail;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 4f);
            yield return new WaitForSeconds(0.8f);
            RecStart("dreamhold");
            recForce = true;
            var anim = hero.GetComponent<tk2dSpriteAnimator>();
            string last = null;
            int changes = 0;
            float y0 = hero.transform.position.y;
            IEnumerator Watch(float seconds, string phase)
            {
                for (float end = Time.time + seconds; Time.time < end;)
                {
                    string clip = anim.CurrentClip != null ? anim.CurrentClip.name : "-";
                    int code = Player.HeroSpecialAnimations.Code(hero);
                    string pose = Player.HeroSpecialAnimations.Decode(code, out int k, out int f) ? $"{k}/{f}" : "无";
                    string now = $"{clip} 表 {pose} 身体 {Player.TerraPlayerSprite.BodyFrameNow}";
                    if (now != last)
                    {
                        changes++;
                        Plugin.Log.LogInfo($"[AutoTest] 梦钉握持 {phase} {Time.time:F2} {clip}#{anim.CurrentFrame} 表 {pose} 身体 {Player.TerraPlayerSprite.BodyFrameNow} 高 {hero.transform.position.y - y0:F2}");
                        last = now;
                    }
                    yield return null;
                }
            }
            Items.DreamNailItem.DevHold = true;
            yield return Watch(3f, "按住");
            Items.DreamNailItem.DevHold = false;
            yield return Watch(1.5f, "松开");
            Items.DreamNailItem.DevHold = null;
            recForce = false;
            yield return RecStop();
            Plugin.Log.LogInfo($"[AutoTest] 梦钉握持 共变化 {changes} 次");
            Player.TerraStats.DevImmortal = false;
        }
    }
}
