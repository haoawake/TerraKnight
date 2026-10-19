using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.0, recorder D): Dirtmouth. The townsfolk moving in as the stages open ("X已到达！" in the chat);
    /// the street with all of them about, walked along; a Terraria townsperson talked to; Elderbug's talk told the
    /// Terraria way, a line a second into the chat, while the hero walks on.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The chat at the bottom left emptied (the lines of what went before off the next clip).</summary>
        static void D4ClearChat() => (HarmonyLib.AccessTools.Field(typeof(Net.NetChat), "lines").GetValue(null) as System.Collections.IList)?.Clear();

        /// <summary>The arrivals alone (suite p4_townarrive_zh): a take of that clip without the rest.</summary>
        bool d4ArriveOnly;

        IEnumerator P4_Dtownarrive(HeroController hero)
        {
            d4ArriveOnly = true;
            yield return P4_Dtown(hero);
        }

        IEnumerator P4_Dtown(HeroController hero)
        {
            yield return P4Kit();
            Shop.DevStage = 0;
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            D4NoAreaTitle();
            var col = hero.GetComponent<BoxCollider2D>();
            var street = TownNpcs.DevStreet;
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(street.x + 3.5f, street.y - bottom + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(3f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 城镇：阶段 0 居民 {TownNpcs.Count}：{TownNpcs.DevDescribe()}");

            // --- They move in: the stages open one after another, each bringing its townsfolk.
            aimInit = false;
            yield return D4Rec("town_arrive");
            float t0 = Time.time;
            int next = 0;
            int[] stages = { 1, 2, 3, 4, 5 };
            Player.TerraMovement.DevMove = 1;
            while (Time.time - t0 < 8f)
            {
                PlayerData.instance.isInvincible = true;
                if (next < stages.Length && Time.time - t0 > 0.8f + next * 1.3f)
                {
                    Shop.DevStage = stages[next++];
                    Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 城镇：阶段 {Shop.UnlockedStage()}，居民 {TownNpcs.Count}");
                }
                // Walk a little, then stand and watch.
                if (Time.time - t0 > 0.9f) Player.TerraMovement.DevMove = 0;
                SmoothAim((Vector2)col.bounds.center + new Vector2(3.5f, 1.4f), 3f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            StopRec();
            Shop.DevStage = -1;
            if (d4ArriveOnly) yield break;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 城镇：全部阶段后 {TownNpcs.Count} 位：{TownNpcs.DevDescribe()}");

            // Those who wait for the townsfolk panel put down too (off the record), then a while to spread out.
            int placed = 0;
            foreach (var def in TownNpcs.AllDefs)
                if (!TownNpcs.Present(def.Internal) && def.Kind != NpcKind.Angler && def.Kind != NpcKind.Visitor)
                {
                    string r = TownNpcs.DevPlace(def.Internal);
                    if (r.StartsWith("放在")) placed++;
                }
            D4ClearChat();
            yield return new WaitForSeconds(9f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 城镇：又安置 {placed} 位，共 {TownNpcs.Count} 位");

            // --- The street with all of them, walked along end to end.
            var xs = new List<float>();
            foreach (var def in TownNpcs.AllDefs)
            {
                var p = TownNpcs.DevPos(def.Internal);
                if (p != null) xs.Add(p.Value.x);
            }
            xs.Sort();
            float startX = xs.Count > 0 ? xs[0] - 4f : street.z + 4f, endX = xs.Count > 0 ? xs[xs.Count - 1] + 3f : street.w - 4f;
            hero.transform.position = Clear(hero, new Vector2(startX, street.y - bottom + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            aimInit = false;
            yield return D4Rec("town_walk");
            t0 = Time.time;
            Player.TerraMovement.DevMove = 1;
            while (Time.time - t0 < 9f && col.bounds.center.x < endX)
            {
                PlayerData.instance.isInvincible = true;
                Vector2 c = col.bounds.center;
                float ahead = endX;
                foreach (var x in xs) if (x > c.x + 1.2f) { ahead = x; break; }
                SmoothAim(new Vector2(ahead, street.y + 1.1f), 4f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.5f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 城镇：走完 {Time.time - t0:F1}s");

            // --- A Terraria townsperson talked to.
            string who = null;
            foreach (var n in new[] { "PartyGirl", "Wizard", "Dryad", "Steampunker", "Guide" })
                if (who == null && TownNpcs.DevPos(n) != null) who = n;
            if (who != null)
            {
                yield return D4BesideNpc(hero, who, -2.2f, TownNpcs.DevStreet.x + 9f);
                PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(0.6f, 1.6f)));
                yield return D4Rec("town_talk");
                yield return new WaitForSeconds(0.3f);
                yield return D4Talk(who);
                var r = InventoryGui.DevHitRect("ChatButton", 0);
                yield return new WaitForSeconds(2.6f);
                if (r != null) { yield return Glide(r.Value.center, 0.5f); yield return new WaitForSeconds(0.6f); }
                StopRec();
                TownNpcs.Close(true);
                PointerOff();
            }

            // --- Elderbug: his lines into the chat a second apart, the hero free to walk on.
            GameObject elder = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Elderbug") { elder = t.gameObject; break; }
            if (elder != null)
            {
                D4ClearChat();
                hero.transform.position = Clear(hero, (Vector2)elder.transform.position + new Vector2(-4.5f, 0.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1.2f);
                aimInit = false;
                yield return D4Rec("town_elderbug");
                // Up to him.
                Player.TerraMovement.DevMove = 1;
                t0 = Time.time;
                while (Time.time - t0 < 2.5f && col.bounds.center.x < elder.transform.position.x - 1.3f)
                {
                    SmoothAim((Vector2)elder.transform.position + new Vector2(0f, 1.2f), 4f);
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(0.4f);
                int told0 = UI.DialogueBroadcast.DevTold;
                for (int i = 0; i < 4 && UI.DialogueBroadcast.DevTold == told0; i++)
                {
                    yield return Tap(UnityEngine.InputSystem.Key.W, 0.2f);
                    for (float w = 0f; w < 0.9f && UI.DialogueBroadcast.DevTold == told0; w += Time.deltaTime) yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 城镇：老虫子 播报 {UI.DialogueBroadcast.DevTold > told0}（{UI.DialogueBroadcast.DevLastConvo}），排队 {UI.DialogueBroadcast.DevWaiting}");
                yield return new WaitForSeconds(1.2f);
                // On past him while the lines keep coming.
                Player.TerraMovement.DevMove = 1;
                t0 = Time.time;
                while (Time.time - t0 < 5f)
                {
                    PlayerData.instance.isInvincible = true;
                    SmoothAim((Vector2)col.bounds.center + new Vector2(4f, 1.2f), 3f);
                    if (Time.time - t0 > 3.2f) Player.TerraMovement.DevMove = 0;
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(1.0f);
                StopRec();
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 城镇：老虫子 说完，还排着 {UI.DialogueBroadcast.DevWaiting}，聊天 {Net.NetChat.DevLastLine}");
            }
            mod.DevAim = null;
            Shop.DevStage = -1;
        }
    }
}
