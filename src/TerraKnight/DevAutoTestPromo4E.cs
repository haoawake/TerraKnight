using System.Collections;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.4, recorder E): the helpers its clips share. Waits and the pointer's glides count frames, not seconds
    /// (a recording runs at 50 frames of game time a second, and the settings and achievements windows stop the time
    /// scale, so a wait in seconds would never end there). A press on the start menu's, the character creation's and the
    /// achievements' buttons is a real mouse press (Windows' messages to this game's window at the drawn pointer: those
    /// screens take IMGUI's own mouse events); one in the inventory is the dev pointer's click (TerraCursor.DevClick).
    /// The player's name over their own lines is the character's while a clip asks for it (a test copy's own is "Test …").
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Frames for a span of the recording's time (50 a second).</summary>
        static int E5Frames(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * 50f));

        /// <summary>A wait of so many frames (the recording's seconds), whatever the time scale.</summary>
        static IEnumerator E5Wait(float seconds)
        {
            int n = E5Frames(seconds);
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Where the clip being recorded is (its seconds), for the catalog.</summary>
        string E5At => recording ? $"{recFrame / 50f:F2}s" : "-";

        void E5Log(string what) => Plugin.Log.LogInfo($"[AutoTest] 录制E @{E5At} {what}");

        /// <summary>A clip begun (a couple of frames on, as D4Rec: no frame written twice), pickup texts of before cleared.</summary>
        IEnumerator E5Rec(string clip)
        {
            yield return null;
            yield return null;
            UI.PopupText.Clear();
            StartRec("v5E_" + clip);
        }

        /// <summary>Called each frame of a glide (the ruler's world point kept on the pointer).</summary>
        System.Action e5EachFrame;

        /// <summary>The drawn pointer glides to a GUI point on a slight curve, easing in and out, over so many frames.</summary>
        IEnumerator E5Glide(Vector2 to, float seconds)
        {
            var from = UI.TerraCursor.DevAt ?? new Vector2(Screen.width * 0.55f, Screen.height * 0.6f);
            var d = to - from;
            var bend = d.sqrMagnitude > 1f ? new Vector2(-d.y, d.x).normalized * Mathf.Min(60f, d.magnitude * 0.1f) : Vector2.zero;
            var ctrl = (from + to) / 2f + bend;
            int n = E5Frames(seconds);
            for (int i = 1; i <= n; i++)
            {
                float u = Mathf.SmoothStep(0f, 1f, i / (float)n);
                UI.TerraCursor.DevAt = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * ctrl + u * u * to;
                e5EachFrame?.Invoke();
                yield return null;
            }
            UI.TerraCursor.DevAt = to;
        }

        /// <summary>A real mouse press and release at a GUI point (the window's own messages, as IMGUI's buttons take a mouse).</summary>
        IEnumerator E5Press(Vector2 p)
        {
            PostMouse(WmMouseMove, p, false);
            yield return null;
            yield return null;
            PostMouse(WmLButtonDown, p, true);
            yield return null;
            yield return null;
            PostMouse(WmLButtonUp, p, false);
            yield return null;
        }

        /// <summary>To a point, a beat on it, a real press there.</summary>
        IEnumerator E5GlidePress(Vector2 p, float glide, float before = 0.18f)
        {
            yield return E5Glide(p, glide);
            yield return E5Wait(before);
            yield return E5Press(p);
        }

        /// <summary>To a point, a beat on it, the dev pointer's click (the inventory's buttons).</summary>
        IEnumerator E5GlideClick(Vector2 p, float glide, float before = 0.15f)
        {
            yield return E5Glide(p, glide);
            yield return E5Wait(before);
            UI.TerraCursor.DevClick();
            yield return null;
            yield return null;
        }

        /// <summary>An inventory button (its kind and index, as last drawn) clicked by the dev pointer; false when it isn't there.</summary>
        IEnumerator E5Hit(string kind, int index, float glide, float before = 0.15f)
        {
            yield return null;
            var r = InventoryGui.DevHitRect(kind, index);
            if (r == null)
            {
                E5Log($"找不到按钮 {kind} {index}");
                yield break;
            }
            yield return E5GlideClick(r.Value.center, glide, before);
        }

        /// <summary>The pointer driving the interface (hover, the dev clicks), drawn at a GUI point.</summary>
        static void E5PointerOn(Vector2 at)
        {
            UI.TerraCursor.DevDrives = true;
            UI.TerraCursor.DevAt = at;
        }

        static void E5PointerOff()
        {
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevHeld = false;
            UI.TerraCursor.DevAt = null;
        }

        /// <summary>The world point under a GUI point (the plane the game is played on).</summary>
        static Vector2 E5World(Vector2 gui)
        {
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return Vector2.zero;
            var ray = cam.ScreenPointToRay(new Vector3(gui.x, Screen.height - gui.y, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin;
            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        /// <summary>The chat at the bottom left emptied, and the talk still waiting to be told in it.</summary>
        static void E5ClearChat()
        {
            const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            (typeof(Net.NetChat).GetField("lines", any)?.GetValue(null) as IList)?.Clear();
            var waiting = typeof(UI.DialogueBroadcast).GetField("waiting", any)?.GetValue(null);
            waiting?.GetType().GetMethod("Clear")?.Invoke(waiting, null);
        }

        // ------------------------------------------------------------------ the player's name over their own lines

        static string e5Name;
        static Harmony e5Harmony;

        /// <summary>The character's name as the player's (the story's lines, the chat) while set; null: the game's own again.</summary>
        static void E5PlayerName(string name)
        {
            e5Name = name;
            if (name == null || e5Harmony != null) return;
            e5Harmony = new Harmony("terraknight.promo.v5e");
            var getter = AccessTools.PropertyGetter(typeof(Net.NetSettings), nameof(Net.NetSettings.PlayerName));
            e5Harmony.Patch(getter, prefix: new HarmonyMethod(typeof(DevAutoTest), nameof(E5NamePrefix)));
        }

        static bool E5NamePrefix(ref string __result)
        {
            if (string.IsNullOrEmpty(e5Name)) return true;
            __result = e5Name;
            return false;
        }

        // ------------------------------------------------------------------ an ordinary save for the clips that need one

        /// <summary>
        /// The showcase gear on an ordinary (not creative) save: achievements count, the story's lines are on, the Master
        /// relics drop. The four class loadouts and lunar gear are given as on a creative save, then the save made ordinary.
        /// </summary>
        IEnumerator E5OrdinaryKit()
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreative(false);
            E5NoInfinite();
            World.GameDifficulty.Current = World.DifficultyLevel.Master;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            // Every area been to: no area's big title card over a clip.
            var pd = PlayerData.instance;
            foreach (var f in typeof(PlayerData).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                if (f.FieldType == typeof(bool) && f.Name.StartsWith("visited")) f.SetValue(pd, true);
            yield return new WaitForSeconds(0.5f);
            E5Log($"普通存档：创造 {mod.Creative}，成就计数 {Achievements.Achievements.Counting}，小剧情 {StoryBeats.On}");
        }

        /// <summary>Stacks spent as in a real game (the autotests' endless items off: no "∞" under the hotbar's stacks).</summary>
        void E5NoInfinite()
        {
            TerraMod.DevAllInfinite = false;
            mod.Inventory.Infinite = false;
        }

        /// <summary>The hero put on Dirtmouth's street, a little right of the bench (the area's title off).</summary>
        IEnumerator E5ToTown(float dx = 7f)
        {
            yield return EnterRoom("Town");
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            D4NoAreaTitle();
            var col = hero.GetComponent<BoxCollider2D>();
            var street = Town.TownNpcs.DevStreet;
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(street.x + dx, street.y - bottom + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
        }
    }
}
