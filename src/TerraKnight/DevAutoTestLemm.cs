using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "lemm"): the Relic Seeker's shop in the City of Tears (Ruins1_05b), photographed whole
    /// with the minimap off: what is under Lemm, the floor by the tall windows.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LemmTest()
        {
            yield return GoTo("Ruins1_05b", EntryGateOf("Ruins1_05b"));
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            UI.WorldMap.DevStyle(0);
            PlayMakerFSM lemm = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "npc_control" && f.gameObject.name.Contains("Relic")) lemm = f;
            if (lemm != null)
            {
                var hero = HeroController.instance;
                hero.transform.position = Clear(hero, (Vector2)lemm.transform.position + new Vector2(-4f, 0f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 莱姆 {(Vector2)lemm.transform.position:F1}，主角 {(Vector2)hero.transform.position:F1} 站地 {hero.cState.onGround}；脚下：{UI.WorldMap.DevNameAtWorld((Vector2)lemm.transform.position + Vector2.down * 1.5f)}");
            }
            Shot("Ruins1_05b", "lemm");
            yield return null;
            UI.WorldMap.DevStyle(1);
        }
    }
}
