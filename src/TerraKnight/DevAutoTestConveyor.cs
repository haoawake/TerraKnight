using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "conveyor"): Crystal Peak's belts. Dropped onto one and left standing, the Knight
    /// should ride at the belt's speed (the game's: its speed added to a standing run), not be flung.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ConveyorTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            foreach (var scene in new[] { "Mines_02", "Mines_04" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(1.5f);
                var belts = new System.Collections.Generic.List<ConveyorBelt>();
                foreach (var b in Object.FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None))
                    if (b.gameObject.scene.name == scene && b.GetComponent<Collider2D>() != null && Mathf.Abs(b.speed) > 0.1f) belts.Add(b);
                if (belts.Count == 0)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 传送带 {scene} 找不到传送带");
                    continue;
                }
                foreach (var belt in belts)
                {
                    if (belt == null || GameManager.instance.sceneName != scene) break;
                    string beltName = belt.name;
                    float beltSpeed = belt.speed;
                    var h = HeroController.instance;
                    var rb = h.GetComponent<Rigidbody2D>();
                    var box = belt.GetComponent<Collider2D>().bounds;
                    h.transform.position = new Vector3(box.center.x, box.max.y + 1.2f, h.transform.position.z);
                    rb.linearVelocity = Vector2.zero;
                    float maxVx = 0f;
                    bool rode = false, zone = false;
                    var frames = new System.Collections.Generic.List<string>();
                    var trace = new System.Text.StringBuilder();
                    string under = "";
                    for (float t = 0f; t < 3f; t += Time.fixedDeltaTime)
                    {
                        yield return new WaitForFixedUpdate();
                        if (h == null || GameManager.instance.sceneName != scene) break;
                        mod.Stats.FullHeal();
                        float vx = rb.linearVelocity.x;
                        if (h.cState.inConveyorZone) zone = true;
                        if (h.cState.onConveyor) { rode = true; maxVx = Mathf.Max(maxVx, Mathf.Abs(vx)); }
                        // Frame by frame while on it or flung, the first 40: what the game and the mod see.
                        if ((h.cState.onConveyor || Mathf.Abs(vx) > Mathf.Abs(beltSpeed) * 1.5f) && frames.Count < 40)
                            frames.Add($"{vx:F1}/{(h.cState.onConveyor ? "带" : "")}{(h.cState.onGround ? "地" : "")}{(h.cState.inConveyorZone ? "区" : "")}/{h.hero_state}/{h.conveyorSpeed:F0}{(h.acceptingInput ? "" : "/无输入")}");
                        if (Mathf.Repeat(t, 0.5f) < Time.fixedDeltaTime) trace.Append($" {vx:F1}{(h.cState.onConveyor ? "带" : "")}{(h.cState.inConveyorZone ? "区" : "")}");
                        if (t > 1f && under == "")
                        {
                            var hb = h.GetComponent<BoxCollider2D>().bounds;
                            var hit = Physics2D.Raycast(new Vector2(hb.center.x, hb.min.y + 0.05f), Vector2.down, 0.5f, 1 << 8 | 1 << World.ChunkColliders.TerrainLayer);
                            under = hit.collider != null ? $"{hit.collider.name}（层 {hit.collider.gameObject.layer}）" : "空";
                        }
                    }
                    // Inside a conveyor zone too the game adds the belt's speed twice (its own FixedUpdate).
                    bool ok = maxVx > 0.1f && maxVx <= Mathf.Abs(beltSpeed) * (zone ? 2f : 1f) * 1.25f + 0.5f;
                    if (h == null || GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 传送带 {scene} {beltName}：主角离开了场景（{GameManager.instance.sceneName}） 不通过"); break; }
                    Plugin.Log.LogInfo($"[AutoTest] 传送带 {scene} {beltName}：带 {box.center:F1} {box.size:F1}，带速 {beltSpeed:F1}{(zone ? "（也在传送区里）" : "")}，站上去最快 {maxVx:F1}（应约等于带速）；脚下 {under}；主角最后在 {(Vector2)h.transform.position:F1}；每半秒{trace} {(ok ? "通过" : rode ? "不通过" : "没站上传送带")}{(ok ? "" : "；逐帧 " + string.Join(" ", frames))}");
                }
            }
        }
    }
}
