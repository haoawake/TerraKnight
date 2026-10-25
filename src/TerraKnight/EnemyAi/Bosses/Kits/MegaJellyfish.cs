using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// Uumuu (Mega Jellyfish; 空洞骑士首领新版AI设计.md §2.3):
    /// <list type="bullet">
    /// <item>its zaps the game's own size, each of its chase's given a little longer to grow than the game's (0.85 s, the
    /// first 1.2 s): time to get out from under it;</item>
    /// <item>each zap of its chase comes where the player is as it is made (the game's own); its patterns a little thinner,
    /// about one zap in five of each left out;</item>
    /// <item>its shell broken, it flees from the player while it can be struck;</item>
    /// <item>a player kept up in the air gets the chasing zaps.</item>
    /// </list>
    /// </summary>
    [Kit("MegaJellyfish")]
    internal sealed class MegaJellyfishKit : BossKit
    {
        protected override string ControlName => "Mega Jellyfish";
        protected override Look Theme => Look.Electric;

        /// <summary>How much longer a chase's zap grows than the game's (0.65 s, the chase's first 1 s).</summary>
        const float MoreAntic = 0.2f;
        /// <summary>The share of a pattern's zaps left out.</summary>
        const float Thinned = 0.2f;

        protected override void Setup()
        {
            Tempo("Idle", "Attack Recover");
            // First? sets the new zap's Antic Time (0.65 s; for the chase's first, 1 s after its test): each one longer.
            At("First?", 4, () => Slower(1f));
            At("First?", 1, () => Slower(0.65f));
            OnEnter("Gen", MarkZap, first: false);
            // Right after its zaps are set off (ActivateAllChildren): some put away again before they grow.
            At("Multizap", 2, ThinPattern);
            OnEnter("Wounded", () => Run(Flee(Godhome ? 3.5f : 3f)), first: false);
            // After the zap count is reset: a player up in the air gets the chase.
            At("Choice", 2, () =>
            {
                if (WantAntiAir() && Control != null) Control.SendEvent("CHASE");
            });
        }

        void Slower(float antic)
        {
            var zap = Go("Zap")?.Value;
            var v = zap != null ? Batch3.FsmOn(zap, "Control")?.FsmVariables.FindFsmFloat("Antic Time") : null;
            if (v != null) v.Value = antic + MoreAntic;
        }

        float RadiusTiles(GameObject zap)
        {
            var c = zap.GetComponentInChildren<Collider2D>(true);
            float r = c != null && c.bounds.extents.x > 0.1f ? c.bounds.extents.x : 2f;
            return r / 0.5f;
        }

        void MarkZap()
        {
            var zap = Go("Zap")?.Value;
            if (zap == null) return;
            // The first of a chase waits longest (First?).
            bool first = !(Bool("First Chaser")?.Value ?? true);
            MarkCircle(zap.transform.position, RadiusTiles(zap), (first ? 1f : 0.65f) + MoreAntic);
        }

        void ThinPattern()
        {
            var pattern = Go("Current Pattern")?.Value;
            if (pattern == null) return;
            var zaps = new List<GameObject>();
            foreach (Transform child in pattern.transform) if (child.gameObject.activeSelf) zaps.Add(child.gameObject);
            int leave = zaps.Count >= 3 ? Mathf.RoundToInt(zaps.Count * Thinned) : 0;
            for (int i = 0; i < leave; i++)
            {
                int k = Random.Range(i, zaps.Count);
                (zaps[i], zaps[k]) = (zaps[k], zaps[i]);
                // Put away as one that has gone off (the game's own zaps turn themselves off after).
                zaps[i].SetActive(false);
            }
            for (int i = leave; i < zaps.Count; i++)
            {
                var antic = Batch3.FsmOn(zaps[i], "Control")?.FsmVariables.FindFsmFloat("Antic Time");
                MarkCircle(zaps[i].transform.position, RadiusTiles(zaps[i]), antic != null && antic.Value > 0.1f ? antic.Value : 0.8f);
            }
        }

        /// <summary>Its shell broken: away from the player, kept in the arena.</summary>
        IEnumerator Flee(float seconds)
        {
            try
            {
                for (float t = 0f; t < seconds && rb != null; t += Time.deltaTime)
                {
                    if (StateNow != "Wounded") yield break;
                    Vector2 away = Me - TargetCenter;
                    if (away.sqrMagnitude < 0.01f) away = Vector2.up;
                    away.Normalize();
                    // Along the edge rather than into it.
                    Vector2 ahead = Me + away * 2f;
                    if (ClampToArena(ahead, 2f) != ahead) away = new Vector2(-away.y, away.x) * Mathf.Sign(Vector2.Dot(new Vector2(-away.y, away.x), Arena.center - Me) + 0.01f);
                    rb.linearVelocity = away * 3.5f;
                    yield return null;
                }
            }
            finally { if (rb != null) rb.linearVelocity = Vector2.zero; }
        }
    }
}
