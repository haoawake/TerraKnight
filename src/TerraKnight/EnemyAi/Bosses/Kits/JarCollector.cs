using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Collector (Control; 空洞骑士首领新版AI设计.md §2.2):
    /// <list type="bullet">
    /// <item>his jars come down where the player is as each is let go (the first on him, the next ones a little either
    /// side), the place marked under them;</item>
    /// <item>back from the roof he comes down at a slant on where the player is (marked), not on a random spot: his answer
    /// to a player kept up in the air, the jump to the roof his move against it;</item>
    /// <item>what the jars let out is a quarter quicker, its waits shorter.</item>
    /// </list>
    /// He goes up into the roof out of sight between jars (Roams).
    /// </summary>
    [Kit("JarCollector")]
    internal sealed class JarCollectorKit : BossKit
    {
        protected override Look Theme => Look.Void;
        protected override bool Roams => true;

        float pounceX;
        bool pouncing;
        float nextScan;

        protected override void Setup()
        {
            // After the game's own pick (and its "too close to the last one" check): the jar on the player.
            At("Spawn", 3, LockJar);
            At("Return Antic", 1, AimPounce);
            OnEnter("Return", Pounce, first: false);
            OnEnter("Slam", () => pouncing = false);
            Branch("Move Choice", "Jump Antic", WantAntiAir);
            Branch("Move Choice None", "Jump Antic", WantAntiAir);
        }

        void LockJar()
        {
            var x = Float("Bottle X");
            if (x == null) return;
            int k = Int("Jars Spawned")?.Value ?? 0;
            float side = k == 0 ? 0f : (k % 2 == 1 ? 1f : -1f) * ((k + 1) / 2) * 2.5f;
            float min = Float("Bottle XL")?.Value ?? Arena.xMin, max = Float("Bottle XR")?.Value ?? Arena.xMax;
            x.Value = Mathf.Clamp(Lock().x + side, min, max);
            MarkGround(new Vector2(x.Value, TargetFeet.y + 0.3f), 3f, 1f);
        }

        void AimPounce()
        {
            var self = Float("Self X");
            if (self == null) return;
            float min = Float("Roof X L")?.Value ?? Arena.xMin, max = Float("Roof X R")?.Value ?? Arena.xMax;
            pounceX = Mathf.Clamp(Lock().x, min, max);
            // A start a little to the room's middle side of him, the fall slanting on to him.
            float side = Mathf.Sign(Arena.center.x - pounceX);
            if (side == 0f) side = 1f;
            self.Value = Mathf.Clamp(pounceX + side * 2.5f, min, max);
            pouncing = true;
            MarkGround(new Vector2(pounceX, TargetFeet.y + 0.3f), 4f, Mathf.Max(Warn(0.5f), 1.1f));
        }

        void Pounce()
        {
            if (!pouncing || rb == null) return;
            float ground = GroundBelow(new Vector2(pounceX, TargetFeet.y + 1f));
            float h = Mathf.Max(1f, transform.position.y - ground);
            float g = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.1f, rb.gravityScale);
            float t = Mathf.Sqrt(2f * h / g);
            float vx = Mathf.Clamp((pounceX - Me.x) / Mathf.Max(0.2f, t), -12f, 12f);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
        }

        protected override void Tick()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.5f;
            // The jars' enemies carry the Boss tag (his own count of them); they come from the global pool, so by place.
            var near = new Rect(Arena.xMin - 4f, Arena.yMin - 4f, Arena.width + 8f, Arena.height + 8f);
            foreach (var go in GameObject.FindGameObjectsWithTag("Boss"))
            {
                if (go == null || go == gameObject || !near.Contains(go.transform.position) || go.GetComponent<HealthManager>() == null) continue;
                Batch3.Hasten(this, go, 1.25f, 0.8f);
            }
        }
    }
}
