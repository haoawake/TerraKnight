using System.Collections;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Soul Warrior (Mage Knight; 空洞骑士首领新版AI设计.md §2.1):
    /// <list type="bullet">
    /// <item>the teleport over the player and slash down lands where he is going now and then (led), marked on the ground;</item>
    /// <item>a dash at the player's height: with him up in the air, the Warrior appears beside him there and dashes across,
    /// then goes back down to his floor; a player on a platform gets him standing on it at its far end, dashing along it;</item>
    /// <item>after a teleport, three times in ten, a soul orb that turns after the player; from the second rank two of them,
    /// and his own orb throw one more;</item>
    /// <item>both his teleports blocked (the player up under the ceiling, no room over or beside him): the dash at his
    /// height, or back to his idle, never round and round between them;</item>
    /// <item>pace ×0.9; a player kept up in the air gets the dash at his height.</item>
    /// </list>
    /// </summary>
    [Kit("MageKnight")]
    internal sealed class SoulWarriorKit : BossKit
    {
        protected override string ControlName => "Mage Knight";
        protected override float Pace => 0.9f;
        protected override Look Theme => Look.Soul;
        /// <summary>His own soul orb (his Shoot's).</summary>
        GameObject orbShot;
        GameObject OrbShot => orbShot != null ? orbShot : orbShot = OwnShots.Of(State("Shoot"));

        protected override void Setup()
        {
            Tempo("Idle", "Shoot CD", "Evade Recover");
            // The hero's place read in (GetPosition, second), then led before the 8 units over him are added.
            At("Up Tele Aim", 2, () =>
            {
                var x = Float("Target X");
                if (x != null && RollLead()) x.Value = Lead(0.7f).x;
            });
            OnEnter("Stomp Antic", () =>
            {
                MarkGround(Me, 4f, Warn(0.5f));
                MaybeOrbs();
            });
            OnEnter("After Side Tele", MaybeOrbs);
            OnEnter("Shoot", () => { if (Rank >= 1) Orb(30f); }, first: false);
            AddState("TK Air Dash", AirDash, "Idle");
            Branch("Move Decision", "TK Air Dash", () => TargetHeight > 2.5f && (WantAntiAir() || Random.value < 0.35f));
            // Over him blocked (Up Tele Aim's CANCEL), then beside him blocked too (Side Tele Aim's): the game goes round the
            // two a frame at a time for as long as it lasts.
            OnEnter("Side Tele Aim", () => sideTried = Time.time);
            Branch("Cancel Frame", "Idle", () => Time.time - sideTried < 0.25f);
            Branch("Cancel Frame", "TK Air Dash", () => HasTarget && TargetHeight > 2.5f);
        }

        float sideTried = -10f;
        void MaybeOrbs()
        {
            if (Random.value >= 0.3f) return;
            Orb(0f);
            if (Rank >= 1) Orb(Random.value < 0.5f ? 35f : -35f);
        }

        /// <summary>A soul orb from him at the player, turned off his way by some degrees, turning after him.</summary>
        void Orb(float turn)
        {
            var dir = TargetCenter - Me;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            dir = Quaternion.Euler(0f, 0f, turn) * dir.normalized;
            OwnShots.Throw(OrbShot, Me + Vector2.up * 0.5f, dir * 8f, fire: true);
        }

        /// <summary>
        /// Where the dash at his height starts: eight across from him, five, three, first on the side the Warrior is on, then
        /// the other, the first with no rock of the room between it and him (along his middle and a half over and under it);
        /// with both sides walled off (under a ledge, by a wall), close beside him. A start behind a ledge left the dash stopped
        /// short on the rock two units off a player up under it (the user's 10-25: aimed at, never hit).
        /// </summary>
        float DashStart(Vector2 t, float y, float side)
        {
            float lo = Arena.xMin + 1.5f, hi = Arena.xMax - 1.5f;
            foreach (float s in new[] { side, -side })
                foreach (float reach in new[] { 8f, 5f, 3f })
                {
                    float x = Mathf.Clamp(t.x + s * reach, lo, hi);
                    float run = Mathf.Abs(t.x - x);
                    if (run < 2f) continue;
                    var dir = new Vector2(Mathf.Sign(t.x - x), 0f);
                    bool clear = !Hostiles.NativeSolidAt(new Vector2(x, y));
                    for (int k = -1; k <= 1 && clear; k++)
                        clear = Hostiles.NativeRay(new Vector2(x, y + k * 0.5f), dir, run).collider == null;
                    if (clear) return x;
                }
            return Mathf.Clamp(t.x + side * 2.5f, lo, hi);
        }

        /// <summary>Beside the player at his height, the line across shown, a dash through, then back down to his floor.</summary>
        IEnumerator AirDash()
        {
            Hostiles.Zone escort = null;
            try
            {
                var t = Lock();
                float side = Me.x < t.x ? -1f : 1f;
                float y = Mathf.Clamp(t.y, GroundBelow(t) + 1.2f, CeilingAbove(t) - 1.2f);
                float x = DashStart(t, y, side);
                Vector2 offset = Me - (Vector2)transform.position;
                // On a platform (the room's or one he built) wide enough: standing on it, at its end farther from him.
                if (FootingSpan(out float left, out float right) && right - left >= 5f)
                {
                    float half = body != null ? body.bounds.extents.y : 1.5f;
                    bool fromRight = t.x < (left + right) * 0.5f;
                    x = Mathf.Clamp(fromRight ? right : left, Arena.xMin + 1.5f, Arena.xMax - 1.5f);
                    y = TargetFeet.y + half + 0.05f;
                }
                transform.position = new Vector3(x - offset.x, y - offset.y, transform.position.z);
                if (rb != null) rb.linearVelocity = Vector2.zero;
                float dir = Mathf.Sign(t.x - x);
                var s = transform.localScale;
                transform.localScale = new Vector3(-dir * Mathf.Abs(s.x), s.y, s.z);
                float warn = Warn(0.5f);
                var from = new Vector2(x, y);
                MarkLine(from, new Vector2(WallTowards(from, dir), y), warn);
                yield return new WaitForSeconds(warn);
                float speed = Mathf.Max(30f, Mathf.Abs(Float("Dash Speed")?.Value ?? 40f));
                Batch2.Play(gameObject, "Dash");
                // His body carries the hurt through the dash (his own dash's look).
                escort = Batch2.Escort(this, transform, Me - (Vector2)transform.position, 3f, 0.55f, Look.Soul);
                for (float time = 0f; time < 0.45f; time += Time.deltaTime)
                {
                    if (rb != null) rb.linearVelocity = new Vector2(dir * speed, 0f);
                    yield return null;
                }
                if (rb != null) rb.linearVelocity = Vector2.zero;
                Batch2.Play(gameObject, "Slash Recover");
                yield return new WaitForSeconds(0.2f);
                // Back down to his floor (he has no gravity: his teleports set him there).
                float floor = Float("Floor Y")?.Value ?? GroundBelow(Me);
                transform.position = new Vector3(transform.position.x, floor, transform.position.z);
            }
            finally
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                if (escort != null) Destroy(escort.gameObject);
            }
        }
    }
}
