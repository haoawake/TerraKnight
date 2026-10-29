using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Hive Knight (Control; 空洞骑士首领新版AI设计.md §2.5):
    /// <list type="bullet">
    /// <item>his roar's bee swarm comes down where the player is as it sets off: a curtain across him, the bees falling
    /// straight with their places marked, gaps between them (the game's own chase after the player taken off);</item>
    /// <item>after a jump he calls a few of the hive's bees (struck down for soul, as ever) straight after the player;</item>
    /// <item>his honey globs rise over where the player is (their burst ringed), a fourth of his own from his second rank;</item>
    /// <item>his stab dash and his teleport slash are lined out first (they go at the player as he was);</item>
    /// <item>against a player kept up in the air he teleports to him (beside him at his height).</item>
    /// </list>
    /// </summary>
    [Kit("HiveKnight")]
    internal sealed class HiveKnightKit : BossKit
    {
        protected override Look Theme => Look.Honey;

        readonly List<PlayMakerFSM> droppers = new List<PlayMakerFSM>();
        readonly Dictionary<PlayMakerFSM, ChaseObjectGround> chases = new Dictionary<PlayMakerFSM, ChaseObjectGround>();
        float swarmAt = -10f, swarmX;
        PlayMakerFSM globs;

        protected override void Setup()
        {
            var scene = gameObject.scene;
            droppers.AddRange(Batch3.SceneFsms(scene, "Bee Dropper", "Control"));
            for (int i = 0; i < droppers.Count; i++)
            {
                var f = droppers[i];
                int index = i;
                var chase = Batch3.Find<ChaseObjectGround>(State("Swarm", f));
                if (chase != null) chases[f] = chase;
                At("Swarm Start", 1, () => SwarmStart(f, index), f);
            }
            var g = Batch3.SceneFsms(scene, "Globs", "Control");
            globs = g.Count > 0 ? g[0] : null;
            if (globs != null) At("Pause", 0, PlaceGlobs, globs);
            foreach (var glob in Batch3.SceneFsms(scene, "Hive Knight Glob", "Control"))
            {
                var own = glob;
                At("Tween Up", 1, () => { var t = Float("Translate", own); if (t != null) t.Value = 0f; }, own);
            }
            OnEnter("Dash Antic", () => LineOut(12f, Warn(0.45f)), first: false);
            OnEnter("TeleIn 1", () => LineOut(10f, 0.6f), first: false);
            foreach (var phase in new[] { "Phase 1", "Phase 2", "Phase 3" }) Branch(phase, "TeleOut 1", WantAntiAir);
        }

        /// <summary>Where he faces: his sprite faces left at a positive x scale.</summary>
        float Facing => transform.localScale.x > 0f ? -1f : 1f;

        void LineOut(float length, float seconds) => MarkLine(Me, Me + new Vector2(Facing * length, 0f), Mathf.Max(seconds, BossAi.MinWarn));

        /// <summary>
        /// A bee setting off from the top: a call's bee after the player (the game's chase), else the roar's curtain
        /// across where the player was as the first of them set off, falling straight down.
        /// </summary>
        void SwarmStart(PlayMakerFSM f, int index)
        {
            var place = Float("Random", f);
            if (place == null) return;
            if (chases.TryGetValue(f, out var chase)) chase.Enabled = false;
            if (Time.time - swarmAt > 1f) { swarmAt = Time.time; swarmX = Lock().x; }
            float x = swarmX + (index - (droppers.Count - 1) / 2f) * 2.5f;
            MarkGround(new Vector2(x, TargetFeet.y + 0.3f), 2f, 1.5f);
            var parent = f.transform.parent;
            place.Value = parent != null ? parent.InverseTransformPoint(new Vector3(x, 0f, 0f)).x : x;
        }


        /// <summary>The three globs set over where the player is (each burst ringed); a fourth of his own from rank 1.</summary>
        void PlaceGlobs()
        {
            float x = Lock().x;
            float[] offsets = { -4f, 0f, 4f };
            string[] names = { "Glob L", "Glob M", "Glob R" };
            for (int i = 0; i < 3; i++)
            {
                var g = Go(names[i], globs)?.Value;
                if (g == null) continue;
                var p = g.transform.position;
                float gx = Mathf.Clamp(x + offsets[i], Arena.xMin + 1f, Arena.xMax - 1f);
                g.transform.position = new Vector3(gx, p.y, p.z);
                MarkCircle(new Vector2(gx, p.y + 9f), 6f, 2.6f);
            }
        }

    }
}
