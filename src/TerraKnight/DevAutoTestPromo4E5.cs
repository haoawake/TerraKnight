using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.4, recorder E, suite "p4_v5E_story_zh"): the story's little moments on an ordinary save (each said once
    /// a save, the player's own lines and a tip in the upper middle and the chat). The Grimm Troupe's tent come to
    /// Dirtmouth, walked toward; the first bench, sat on (its pylon rises beside it); a pitch-dark room walked into.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>A story line said since a count: its lines and tip run out (quiet a moment after).</summary>
        static IEnumerator E5StoryOver(int said0, float most)
        {
            float t0 = Time.time;
            while (Time.time - t0 < most && (StoryBeats.DevSaid == said0 || StoryBeats.Talking)) yield return null;
        }

        IEnumerator P4_v5E_story(HeroController hero)
        {
            yield return E5OrdinaryKit();
            E5PlayerName("泰拉旅人");
            var pd = PlayerData.instance;
            pd.troupeInTown = true;
            StoryBeats.SetOn(false);
            yield return E5ToTown(-10f);
            hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            E5ClearChat();
            E5Log($"小剧情 开 {StoryBeats.On}，已说 {StoryBeats.DevSaid}（{StoryBeats.DevLast}）");

            // ---- The Grimm Troupe's tent: the hero walks east along the street while the line comes.
            int said0 = StoryBeats.DevSaid;
            E5PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(3f, 1.2f)));
            UI.TerraCursor.DevDrives = false;
            // The troupe's tent: where it stands (the east end of the town).
            float tentX = float.MinValue;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.gameObject.scene.name != "Town") continue;
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("grimm") && !n.Contains("tent") && !n.Contains("troupe")) continue;
                if (t.parent != null && t.parent.name.ToLowerInvariant().Contains("grimm")) continue;
                E5Log($"小剧情 剧团物体：{t.name} 在 {t.position:F1} 激活 {t.gameObject.activeInHierarchy}");
                if (t.gameObject.activeInHierarchy && n.Contains("tent")) tentX = Mathf.Max(tentX, t.position.x);
            }
            // The hero a little east of the tent, walking up to it as the line comes.
            var street = Town.TownNpcs.DevStreet;
            if (tentX == float.MinValue) tentX = street.z + 26f;
            {
                float bottom = col.bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(tentX + 8f, street.y - bottom + 0.3f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceLeft();
                yield return new WaitForSeconds(1.2f);
                UI.TerraCursor.DevAt = GuiOf((Vector2)col.bounds.center + new Vector2(-3f, 1.2f));
            }
            yield return E5Rec("story_troupe");
            yield return new WaitForSeconds(0.6f);
            StoryBeats.SetOn(true);
            float t0 = Time.time;
            float goal = tentX - 5f;
            while (Time.time - t0 < 24f)
            {
                PlayerData.instance.isInvincible = true;
                float since = Time.time - t0;
                bool walk = col.bounds.center.x > goal && (since < 2.5f || (since % 5f) < 1.6f);
                Player.TerraMovement.DevMove = walk ? -1 : 0;
                UI.TerraCursor.DevAt = Vector2.Lerp(UI.TerraCursor.DevAt.Value, GuiOf((Vector2)col.bounds.center + new Vector2(-3.5f, 1.6f)), 1f - Mathf.Exp(-3f * Time.deltaTime));
                if (since > 4f && StoryBeats.DevSaid > said0 && !StoryBeats.Talking) break;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.8f);
            StopRec();
            E5Log($"小剧情 剧团：说了 {StoryBeats.DevSaid - said0} 句，最后「{StoryBeats.DevLast}」");

            // ---- The first bench: walked to, sat on.
            PlayMakerFSM bench = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Bench Control" && f.gameObject.scene.name == "Town") { bench = f; break; }
            if (bench != null)
            {
                var bp = (Vector2)bench.transform.position;
                float bottom = col.bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(bp.x - 5.5f, bp.y - bottom + 0.3f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1.5f);
                E5ClearChat();
                said0 = StoryBeats.DevSaid;
                UI.TerraCursor.DevAt = GuiOf((Vector2)col.bounds.center + new Vector2(2.5f, 1.5f));
                StoryBeats.SetOn(true);
                yield return E5Rec("story_bench");
                yield return new WaitForSeconds(0.4f);
                Player.TerraMovement.DevMove = 1;
                while (col.bounds.center.x < bp.x - 0.05f)
                {
                    UI.TerraCursor.DevAt = Vector2.Lerp(UI.TerraCursor.DevAt.Value, GuiOf((Vector2)col.bounds.center + new Vector2(2.5f, 1.4f)), 1f - Mathf.Exp(-4f * Time.deltaTime));
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(0.25f);
                for (int k = 0; k < 3 && !pd.atBench; k++)
                {
                    yield return Tap(Key.W, 0.3f);
                    if (!pd.atBench) yield return Tap(Key.UpArrow, 0.3f);
                    for (float w = 0f; w < 1.2f && !pd.atBench; w += Time.deltaTime) yield return null;
                }
                E5Log($"小剧情 长椅：坐下 {pd.atBench}");
                t0 = Time.time;
                while (Time.time - t0 < 26f)
                {
                    if (Time.time - t0 > 3f && StoryBeats.DevSaid > said0 && !StoryBeats.Talking) break;
                    yield return null;
                }
                yield return new WaitForSeconds(1.0f);
                StopRec();
                E5Log($"小剧情 长椅：说了 {StoryBeats.DevSaid - said0} 句，最后「{StoryBeats.DevLast}」");
                // Up again.
                for (int k = 0; k < 3 && pd.atBench; k++)
                {
                    yield return Tap(Key.Space, 0.2f);
                    yield return new WaitForSeconds(1f);
                }
            }

            // ---- A pitch-dark room.
            string dark = null;
            StoryBeats.SetOn(false);
            foreach (var room in new[] { "Mines_33", "Deepnest_01b", "Deepnest_02", "Deepnest_16", "Abyss_04" })
            {
                yield return EnterRoom(room);
                var gm = GameManager.instance;
                int level = gm.sm != null ? gm.sm.darknessLevel : -1;
                E5Log($"小剧情 暗房：{room} 暗度 {level}");
                if (level == 2) { dark = room; break; }
            }
            if (dark != null)
            {
                hero = HeroController.instance;
                col = hero.GetComponent<BoxCollider2D>();
                // Back out to its edge and in again, the line coming as the dark closes in.
                var c = (Vector2)col.bounds.center;
                said0 = StoryBeats.DevSaid;
                E5ClearChat();
                UI.TerraCursor.DevAt = GuiOf(c + new Vector2(2.5f, 1.2f));
                yield return E5Rec("story_dark");
                yield return new WaitForSeconds(0.6f);
                StoryBeats.SetOn(true);
                const int terrain = 1 << ChunkColliders.TerrainLayer;
                int dir = Physics2D.Raycast(c, Vector2.right, 6f, terrain).collider == null ? 1 : -1;
                t0 = Time.time;
                while (Time.time - t0 < 18f)
                {
                    PlayerData.instance.isInvincible = true;
                    float since = Time.time - t0;
                    var cc = (Vector2)col.bounds.center;
                    bool blocked = Physics2D.Raycast(cc, Vector2.right * dir, 1.2f, terrain).collider != null;
                    Player.TerraMovement.DevMove = !blocked && (since < 2.5f || (since % 4f) < 1.2f) ? dir : 0;
                    UI.TerraCursor.DevAt = Vector2.Lerp(UI.TerraCursor.DevAt.Value, GuiOf(cc + new Vector2(dir * 3f, 1f)), 1f - Mathf.Exp(-3f * Time.deltaTime));
                    if (since > 3f && StoryBeats.DevSaid > said0 && !StoryBeats.Talking) break;
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(0.6f);
                StopRec();
                E5Log($"小剧情 暗房：说了 {StoryBeats.DevSaid - said0} 句，最后「{StoryBeats.DevLast}」");
            }
            E5PointerOff();
            E5PlayerName(null);
        }
    }
}
