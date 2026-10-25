using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "animreal" and "animrealdream"): the character-only cutscene poses where the game itself plays them, with
    /// what goes with them (the user: "你去原版真正用这些动画的地方录给我看，我要看伴随的特效"). A recorder runs beside each
    /// scene: while a pose shows (Player.HeroSpecialAnimations), for 1.5 s after, and through forced windows (a pickup's
    /// lead-in), a screenshot every other frame, logged with the pose and the Knight's place on the screen for cropping:
    /// the Vengeful Spirit taken in the Snail Shaman's temple, the temple's charm picked up, Brooding Mawlek's waking roar,
    /// the Dreamgate set and taken; "animrealdream": a Dreamer drained by focus in Lurien's dream (DreamersTest).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] poseSheets = { "absorb_magic", "wake_ground", "focus", "collect_ability", "read_map", "roar_stagger", "dream_warp" };
        bool recOn, recForce;
        string recTag = "";
        int recShots;

        IEnumerator PoseRecorder()
        {
            float lastPose = -10f;
            int frame = 0;
            string sheet = "none";
            while (recOn)
            {
                yield return null;
                var hero = HeroController.SilentInstance;
                var gm = GameManager.UnsafeInstance;
                if (hero == null || gm == null) continue;
                int code = Player.HeroSpecialAnimations.Code(hero);
                bool special = Player.HeroSpecialAnimations.Decode(code, out int kind, out int f);
                var anim = hero.GetComponent<tk2dSpriteAnimator>();
                string clip = anim != null && anim.CurrentClip != null ? anim.CurrentClip.name + "#" + anim.CurrentFrame : "-";
                Vector3 at = hero.GetComponent<BoxCollider2D>().bounds.center;
                // A pickup's own Knight (Knight Get Fireball) stands in while the hero is hidden.
                if (!special && Player.HeroCutsceneDouble.DevShown(out int dcode, out string dclip, out Vector3 feet))
                {
                    special = Player.HeroSpecialAnimations.Decode(dcode, out kind, out f);
                    clip = dclip;
                    at = feet + Vector3.up * 1.2f;
                }
                if (special) { lastPose = Time.time; sheet = poseSheets[kind]; }
                if (!(special || recForce || Time.time - lastPose < 1.5f)) continue;
                if (++frame % 2 != 0 || recShots >= 600) continue;
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : Camera.main;
                var p = cam != null ? cam.WorldToScreenPoint(at) : Vector3.zero;
                string name = $"rec_{recTag}_{recShots:000}";
                Shot(gm.sceneName, name);
                var knight = hero.GetComponent<MeshRenderer>();
                string dbl = Player.HeroCutsceneDouble.DevShown(out int dc, out string dn, out Vector3 df) ? $"{dn}@{(Vector2)df:F1}" : "无";
                Plugin.Log.LogInfo($"[AutoTest] 实录 帧 {name} 场景 {gm.sceneName} 表 {(special ? sheet + "/" + f : "无")} 片段 {clip} 主角屏幕 {p.x:F0},{Screen.height - p.y:F0} 本体渲染 {(knight != null && knight.enabled)} 替身 {dbl}");
                recShots++;
            }
        }

        void RecStart(string tag)
        {
            recTag = tag;
            recShots = 0;
            if (!recOn) { recOn = true; mod.StartCoroutine(PoseRecorder()); }
        }

        IEnumerator RecStop()
        {
            yield return new WaitForSeconds(1.6f);
            recOn = false;
            recForce = false;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 实录 {recTag} 截了 {recShots} 张");
        }

        IEnumerator AnimRealTest(bool spellOnly = false)
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;

            // 1. The Vengeful Spirit, as the Snail Shaman leaves it out: the orb let out, the Knight on it.
            yield return GoTo("Crossroads_ShamanTemple", EntryGateOf("Crossroads_ShamanTemple"));
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            pd.fireballLevel = 0;
            GameObject orb = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.gameObject.scene.name == "Crossroads_ShamanTemple" && f.FsmName == "Vengeful Spirit") orb = f.gameObject;
            if (orb != null)
            {
                RecStart("spell");
                recForce = true;
                orb.SetActive(true);
                yield return new WaitForSeconds(0.5f);
                hero.transform.position = Clear(hero, (Vector2)orb.transform.position + Vector2.down * 0.6f);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                float until = Time.time + 45f, down = -1f, nextTap = 0f;
                bool got = false;
                var heroAnim = hero.GetComponent<tk2dSpriteAnimator>();
                while (Time.time < until)
                {
                    if (pd.fireballLevel > 0) got = true;
                    if (got && GameHooks.PlayerHasControl()) break;
                    // Lying after it (Prostrate): up on a key, as a player gets up.
                    if (down < 0f && heroAnim.CurrentClip != null && heroAnim.CurrentClip.name == "Prostrate") down = Time.time;
                    if (down > 0f && Time.time - down > 2.5f && Time.time >= nextTap) { nextTap = Time.time + 1.5f; mod.StartCoroutine(Tap(Key.Space, 0.1f)); }
                    yield return null;
                }
                recForce = false;
                yield return RecStop();
                Plugin.Log.LogInfo($"[AutoTest] 实录 复仇之魂 吸收 {pd.fireballLevel > 0}");
            }
            else Plugin.Log.LogInfo("[AutoTest] 实录 复仇之魂 找不到光球");
            if (spellOnly) { Player.TerraStats.DevImmortal = false; Plugin.Log.LogInfo("[AutoTest] 实录 结果 完成"); yield break; }

            // 2. The temple's charm (a Shiny Item), inspected as pressing up by it does.
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            PlayMakerFSM shiny = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.gameObject.scene.name == "Crossroads_ShamanTemple" && f.FsmName == "Shiny Control" && f.ActiveStateName == "Idle") shiny = f;
            if (shiny != null)
            {
                for (float wait = Time.time + 10f; Time.time < wait && !GameHooks.PlayerHasControl();) yield return null;
                hero.transform.position = Clear(hero, (Vector2)shiny.transform.position);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                var vars = new System.Text.StringBuilder();
                foreach (var v in shiny.FsmVariables.StringVariables) vars.Append($" {v.Name}={v.Value}");
                foreach (var v in shiny.FsmVariables.BoolVariables) vars.Append($" {v.Name}={v.Value}");
                Plugin.Log.LogInfo($"[AutoTest] 实录 护符 物品{vars} 有控制 {GameHooks.PlayerHasControl()}");
                RecStart("charm");
                recForce = true;
                // Up pressed by it, as a player picks it up.
                KeysDown(Key.W);
                yield return new WaitForSeconds(0.3f);
                KeysDown();
                float until = Time.time + 25f;
                float started = Time.time;
                string lastState = null;
                while (Time.time < until && (Time.time - started < 2f || !GameHooks.PlayerHasControl() || (shiny != null && shiny.ActiveStateName != "Idle" && shiny.ActiveStateName != "Finish")))
                {
                    string now = shiny != null ? shiny.ActiveStateName : "(没了)";
                    if (now != lastState) { Plugin.Log.LogInfo($"[AutoTest] 实录 护符 状态 {Time.time - started:F2}s {now}"); lastState = now; }
                    // The charm's message waits for a key (Wait for Input): one pressed now and then.
                    if (Time.time - started > 3f && Mathf.Repeat(Time.time - started, 1.5f) < Time.deltaTime) mod.StartCoroutine(Tap(Key.Space, 0.1f));
                    yield return null;
                }
                recForce = false;
                yield return RecStop();
                Plugin.Log.LogInfo($"[AutoTest] 实录 护符 拾取后状态 {shiny?.ActiveStateName}");
            }
            else Plugin.Log.LogInfo("[AutoTest] 实录 护符 找不到可捡的护符");

            // 3. Brooding Mawlek's waking roar (the first boss whose start holds the Knight: Mawlek Control's Wake Roar,
            // found by the roarprobe suite; the False Knight's entrance has none): walked into the fight's trigger from the right.
            Items.BossArena.DevSetBeaten("Mawlek", false);
            yield return GoTo("Crossroads_09", "right1");
            for (float wait = Time.time + 8f; Time.time < wait && !GameHooks.PlayerHasControl();) yield return null;
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            Collider2D zone = null;
            PlayMakerFSM mawlek = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (!f.gameObject.scene.name.StartsWith("Crossroads_09")) continue;
                if (f.gameObject.name == "Battle Scene" && zone == null) zone = f.GetComponent<Collider2D>();
                if (f.FsmName == "Mawlek Control") mawlek = f;
            }
            Plugin.Log.LogInfo($"[AutoTest] 实录 毛里克 主角 {(Vector2)hero.transform.position:F1} 战斗区 {(zone != null ? zone.bounds.min.ToString("F1") + "~" + zone.bounds.max.ToString("F1") : "没有")} 状态 {mawlek?.ActiveStateName}");
            RecStart("roar");
            recForce = true;
            bool roared = false;
            float step = 0f, note = 0f, aim = zone != null ? zone.bounds.center.x : hero.transform.position.x - 20f;
            for (float until = Time.time + 30f; Time.time < until;)
            {
                if (Time.time >= note)
                {
                    note = Time.time + 2f;
                    Plugin.Log.LogInfo($"[AutoTest] 实录 毛里克 主角 {(Vector2)hero.transform.position:F1} 状态 {mawlek?.ActiveStateName} 片段 {hero.GetComponent<tk2dSpriteAnimator>().CurrentClip?.name}");
                }
                var c = hero.GetComponent<tk2dSpriteAnimator>().CurrentClip;
                if (c != null && (c.name == "Roar Lock" || c.name == "Stun")) roared = true;
                else if (roared && GameHooks.PlayerHasControl()) break;
                if (!roared && Time.time >= step && hero.transform.position.x > aim && GameHooks.PlayerHasControl())
                {
                    step = Time.time + 0.25f;
                    hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 0.8f);
                }
                yield return null;
            }
            yield return RecStop();
            Plugin.Log.LogInfo($"[AutoTest] 实录 毛里克 吼叫 {roared}");

            // 4. The Dreamgate: set in Dirtmouth (Dream Nail held with S), taken from the Crossroads (with W), as the
            // dreamnail suite does.
            pd.hasDreamNail = true;
            pd.hasDreamGate = true;
            if (pd.dreamOrbs < 10) pd.dreamOrbs = 10;
            int nail = SlotOf(Items.ItemType.DreamNail);
            if (nail < 0) { mod.Inventory.Slots[0] = Items.ItemStack.Of(Items.ItemType.DreamNail); nail = 0; }
            mod.Inventory.Selected = nail;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 4f);
            yield return new WaitForSeconds(0.8f);
            RecStart("dreamgate");
            recForce = true;
            KeysDown(Key.S);
            yield return new WaitForSeconds(0.15f);
            Items.DreamNailItem.DevHold = true;
            yield return new WaitForSeconds(3f);
            Items.DreamNailItem.DevHold = false;
            yield return new WaitForSeconds(0.3f);
            Items.DreamNailItem.DevHold = null;
            KeysDown();
            yield return new WaitForSeconds(1.2f);
            recForce = false;
            yield return RecStop();
            Plugin.Log.LogInfo($"[AutoTest] 实录 梦之门 放门 后 主角 {(Vector2)HeroController.instance.transform.position:F1} 门 {pd.dreamGateScene} {pd.dreamGateX:F1},{pd.dreamGateY:F1}");

            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            RecStart("dreamwarp");
            recForce = true;
            KeysDown(Key.W);
            yield return new WaitForSeconds(0.15f);
            Items.DreamNailItem.DevHold = true;
            yield return new WaitForSeconds(4.3f);
            Items.DreamNailItem.DevHold = false;
            KeysDown();
            for (float until = Time.time + 20f; Time.time < until && (GameManager.instance.sceneName != pd.dreamGateScene || !GameHooks.PlayerHasControl());) yield return null;
            Items.DreamNailItem.DevHold = null;
            yield return new WaitForSeconds(0.8f);
            recForce = false;
            yield return RecStop();
            Plugin.Log.LogInfo($"[AutoTest] 实录 梦之门 传送 后 场景 {GameManager.instance.sceneName} 主角 {(Vector2)HeroController.instance.transform.position:F1} 精华 {pd.dreamOrbs}");
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo("[AutoTest] 实录 结果 完成");
        }

        IEnumerator AnimRealDreamTest()
        {
            Player.TerraStats.DevImmortal = true;
            RecStart("dreamer");
            yield return DreamersTest("Ruins2_Watcher_Room");
            yield return RecStop();
            Plugin.Log.LogInfo("[AutoTest] 实录 结果 完成");
        }
    }
}
