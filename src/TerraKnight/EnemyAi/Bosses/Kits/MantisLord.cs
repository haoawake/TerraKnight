using System.Collections;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Mantis Lords and the Sisters of Battle ("Mantis Lord" on each body; the second phase's pair led by Battle Sub's
    /// "Start"; 空洞骑士首领新版AI设计.md §2.2):
    /// <list type="bullet">
    /// <item>a little quicker (×0.88 on their waits);</item>
    /// <item>the plunge from above falls where the player was as it shows (no steering after him), the spot marked;</item>
    /// <item>new, the ceiling drop: a lord up on the ceiling, upside down a moment, then slashing down at a slant through
    /// where the player was (the line shown) to the floor;</item>
    /// <item>new, the wall slash: a lord on a wall dashing at a slant through where the player was (the line shown);</item>
    /// <item>the pair: the plunge follows the dash at once (the second lord down on the first one's mark);</item>
    /// <item>the dash at the player's level: a player up on a platform (the room's or one he built) gets it along that
    /// platform, the lord coming in at its far end; a player up in the air (hanging under the ceiling) gets it across the
    /// room at his height.</item>
    /// </list>
    /// Against the air: the dash at his level, the ceiling drop and the wall slash.
    /// </summary>
    [Kit("MantisLord")]
    internal sealed class MantisLordKit : BossKit
    {
        protected override string ControlName => "Mantis Lord";
        protected override float Pace => 0.88f;
        protected override Look Theme => Look.Blade;
        // They leave through the walls and wait out of sight between their passes (Leave Pause): not lost out there.
        protected override bool Roams => true;

        bool Sub => Bool("Sub")?.Value ?? false;
        float LandY => Float("Land Y")?.Value ?? GroundBelow(Me) + 1f;
        float XMin => Float("Dstab X Min")?.Value ?? Arena.xMin + 2f;
        float XMax => Float("Dstab X Max")?.Value ?? Arena.xMax - 2f;
        bool ceilingLanding;
        SetPosition landPosition;
        FsmFloat landingY;
        // The dash's own ends, height and side thresholds while one runs at the player's level.
        bool levelled, levelNext;
        float savedXL, savedXR, savedY, savedHeroL, savedHeroR;

        protected override void Setup()
        {
            if (!Batch2.Claim(Control)) { Dormant = true; return; }
            Tempo("Idle", "Throw CD", "Throw CD 2", "After Throw Pause", "Leave Pause");
            // The plunge holds its line.
            Batch2.Disable<ChaseObjectGround>(State("Dstab Attack"));
            OnEnter("Dstab Arrive", LockPlunge);
            OnEnter("Away", Straighten);
            OnEnter("Idle", Straighten);
            landPosition = Batch1Helpers.First<SetPosition>(State("Dstab Land"));
            OnEnter("Dstab Land", PrepareLanding);
            OnExit("Dstab Land", RestoreLanding);
            if (AddState("TK Ceiling Drop", CeilingDrop, "Dstab Land") != null)
            {
                Branch("Attack Choice", "TK Ceiling Drop", () => !Sub && (WantAntiAir() || Random.value < 0.15f + 0.05f * Rank));
                Branch("Dstab Pos", "TK Ceiling Drop", () => Sub && (AirCamping || Random.value < 0.3f));
            }
            if (AddState("TK Wall Slash", WallSlash, "Dash Leave") != null)
            {
                Branch("Attack Choice", "TK Wall Slash", () => !Sub && Random.value < 0.18f);
                Branch("Dash Dir", "TK Wall Slash", () => Sub && (AirCamping || Random.value < 0.25f));
            }
            // A player up high turns the game's choice to its high throw every time: the dash at his level gets its turn first.
            Branch("Attack Choice", "Dash Dir", () =>
            {
                if (Sub || !HasTarget || Grace || Random.value >= 0.5f) return false;
                var y = Float("Dash Y");
                if (y == null || TargetFeet.y - GroundBelow(new Vector2(TargetFeet.x, y.Value + 0.5f)) < 2.5f) return false;
                levelNext = true;
                return true;
            });
            OnEnter("Dash Dir", LevelDash);
            OnEnter("Dash Leave", Unlevel);
            OnEnter("Away", Unlevel);
            Pair();
        }

        /// <summary>
        /// A player well up off the floor: most of the time the dash comes at his level, along the platform he stands on
        /// from its far end (on across the room past it), or across the room at his height in the air.
        /// </summary>
        void LevelDash()
        {
            Unlevel();
            bool chosen = levelNext;
            levelNext = false;
            if (!HasTarget || Grace) return;
            FsmFloat xl = Float("Dash X L"), xr = Float("Dash X R"), y = Float("Dash Y"), hl = Float("Dash Hero L"), hr = Float("Dash Hero R");
            if (xl == null || xr == null || y == null || hl == null || hr == null) return;
            float lo = Mathf.Min(xl.Value, xr.Value), hi = Mathf.Max(xl.Value, xr.Value);
            float floor = GroundBelow(new Vector2((lo + hi) * 0.5f, y.Value + 0.5f));
            if (TargetFeet.y - floor < 2.5f) return;
            bool standing = FootingSpan(out float left, out float right);
            if (!chosen && Random.value >= (standing ? 0.6f : 0.7f)) return;
            float ceil = Mathf.Min(CeilingAbove(TargetFeet + Vector2.up * 0.3f), Arena.yMax);
            levelled = true;
            savedXL = xl.Value; savedXR = xr.Value; savedY = y.Value; savedHeroL = hl.Value; savedHeroR = hr.Value;
            if (standing || !measured) y.Value = Mathf.Min(TargetFeet.y + (savedY - floor), ceil - 1f);
            // Up in the air: its own body (what the dash hurts with) through his middle, kept under the ceiling by its own top;
            // the floor's height offset ran it a tenth of a unit past a player held up there (the user's 10-25).
            else y.Value = Mathf.Min(TargetCenter.y - bodyMiddle, ceil - bodyTop - 0.05f);
            // In from the end farther from him (the platform's, or the room's in the air).
            bool fromRight = TargetFeet.x < (standing ? (left + right) * 0.5f : (lo + hi) * 0.5f);
            if (standing)
            {
                if (fromRight) xr.Value = Mathf.Clamp(right + 0.5f, lo, hi);
                else xl.Value = Mathf.Clamp(left - 0.5f, lo, hi);
            }
            // Dash Dir: the hero left of "Dash Hero L" brings her in from the right (RIGHT, Dash L); right of "Dash Hero R", from the left.
            hl.Value = hr.Value = fromRight ? 1e6f : -1e6f;
            BossAi.DevAntiAir++;
        }

        /// <summary>Its body's middle and top over its position, measured while it is on (off while it waits out of sight).</summary>
        float bodyMiddle, bodyTop;
        bool measured;

        protected override void Tick()
        {
            if (body == null || !body.enabled || body.bounds.size.y < 0.5f || transform.rotation != Quaternion.identity) return;
            bodyMiddle = body.bounds.center.y - transform.position.y;
            bodyTop = body.bounds.max.y - transform.position.y;
            measured = true;
        }

        void Unlevel()
        {
            if (!levelled) return;
            levelled = false;
            FsmFloat xl = Float("Dash X L"), xr = Float("Dash X R"), y = Float("Dash Y"), hl = Float("Dash Hero L"), hr = Float("Dash Hero R");
            if (xl != null) xl.Value = savedXL;
            if (xr != null) xr.Value = savedXR;
            if (y != null) y.Value = savedY;
            if (hl != null) hl.Value = savedHeroL;
            if (hr != null) hr.Value = savedHeroR;
        }

        /// <summary>The second phase's leader: its plunge right after the dash, its waits a little shorter.</summary>
        void Pair()
        {
            PlayMakerFSM battle = null;
            foreach (var f in GetComponentsInParent<PlayMakerFSM>(true))
                if (f.FsmName == "Start" && f.FsmVariables.FindFsmGameObject("Mantis 1") != null) { battle = f; break; }
            if (battle == null || !Batch2.Claim(battle)) return;
            // The Sisters' battle has its moves twice (three of them up, then two): each "Dash Stab", "Stab Dash 2" and so on.
            foreach (var st in battle.Fsm.States)
            {
                string n = st.Name;
                if (n.StartsWith("Dash Stab"))
                {
                    foreach (var a in st.Actions)
                        if (a is SendEventByName e && e.sendEvent != null && e.sendEvent.Value == "DSTAB" && e.delay != null) e.delay.Value = 0.45f;
                    TempoFixed(battle, n, 0.8f);
                }
                else if (n.StartsWith("Stab Dash") || n.StartsWith("Double Stab") || n.StartsWith("Triple Stab") || n.StartsWith("D Dash L ")
                         || n.StartsWith("D Dash R ") || n.StartsWith("D Throw") || n.StartsWith("Do Pincer"))
                    TempoFixed(battle, n, 0.9f);
            }
        }

        void Straighten() => transform.rotation = Quaternion.identity;

        void PrepareLanding()
        {
            if (!ceilingLanding || landPosition == null) return;
            ceilingLanding = false;
            landingY = landPosition.y;
            // The native landing state also assigns Land Y. A custom slanted dive may stop
            // against terrain earlier; retain its actual height for this single landing.
            landPosition.y = new FsmFloat { Value = transform.position.y };
        }

        void RestoreLanding()
        {
            if (landPosition != null && landingY != null) landPosition.y = landingY;
            landingY = null;
            ceilingLanding = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RestoreLanding();
            Unlevel();
        }

        /// <summary>The plunge's spot: where the player is as the lord shows above, kept.</summary>
        void LockPlunge()
        {
            if (Grace) return;
            float x = Mathf.Clamp(Lock().x, XMin, XMax);
            var p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
            MarkGround(new Vector2(x, p.y), 4f, Warn(0.5f));
        }

        /// <summary>Up on the ceiling beside the player, upside down a moment, then down at a slant through where he was.</summary>
        IEnumerator CeilingDrop()
        {
            ceilingLanding = false;
            Quaternion rotation = transform.rotation;
            Hostiles.Zone escort = null;
            try
            {
                var go = gameObject;
                float landY = LandY, xMin = XMin, xMax = XMax;
                Vector2 seen = TargetCenter;
                float side = Random.value < 0.5f ? -1f : 1f;
                float hx = Mathf.Clamp(seen.x + side * Random.Range(3f, 6f), xMin, xMax);
                float ceil = Mathf.Min(CeilingAbove(new Vector2(hx, landY + 2f), 30f), Arena.yMax) - 1.6f;
                if (ceil < landY + 5f) ceil = landY + 7f;
                transform.position = new Vector3(hx, ceil, transform.position.z);
                transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                Batch2.Show(go, true);
                Batch2.Play(go, "Dstab Arrive");
                float hang = Antic(0.4f);
                Vector2 target = Lock();
                Vector2 from = transform.position;
                Vector2 dir = target - from;
                if (dir.y > -1f) dir.y = -1f;
                dir.Normalize();
                float k = (landY - from.y) / Mathf.Min(-0.1f, dir.y);
                Vector2 end = from + dir * k;
                end.x = Mathf.Clamp(end.x, xMin, xMax);
                MarkLine(from, end, hang + 0.05f);
                MarkGround(end, 4f, hang + 0.3f);
                yield return new WaitForSeconds(hang);
                dir = (end - from).normalized;
                transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.down, dir));
                Batch2.Play(go, "Dstab Attack");
                const float speed = 48f;
                float time = Vector2.Distance(from, end) / speed;
                escort = Batch2.Escort(this, transform, Vector2.zero, 3f, time + 0.15f, Look.Blade);
                yield return Batch2.Travel(this, rb, end, speed, time + 0.15f);
                ceilingLanding = true;
            }
            finally
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                transform.rotation = rotation;
                if (escort != null) Destroy(escort.gameObject);
            }
        }

        /// <summary>On a wall, then a dash at a slant through where the player was, on to the floor, the ceiling or the far wall.</summary>
        IEnumerator WallSlash()
        {
            Quaternion rotation = transform.rotation;
            Hostiles.Zone escort = null;
            try
            {
                var go = gameObject;
                float wl = Float("Wall X L")?.Value ?? Arena.xMin + 1f, wr = Float("Wall X R")?.Value ?? Arena.xMax - 1f;
                float yMin = Float("Wall Y Min")?.Value ?? LandY + 2f, yMax = Float("Wall Y Max")?.Value ?? LandY + 7f;
                float landY = LandY;
                bool left = TargetCenter.x > (wl + wr) * 0.5f;
                float x = left ? wl : wr;
                var p = transform.position;
                transform.position = new Vector3(x, Random.Range(yMin, yMax), p.z);
                var sc = transform.localScale;
                transform.localScale = new Vector3(left ? -Mathf.Abs(sc.x) : Mathf.Abs(sc.x), sc.y, sc.z);
                Batch2.Show(go, true);
                Batch2.Play(go, "Wall Arrive");
                yield return new WaitForSeconds(0.25f);
                float antic = Antic(0.45f);
                Vector2 target = Lock();
                Vector2 from = transform.position;
                Vector2 dir = (target - from).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = new Vector2(left ? 1f : -1f, 0f);
                float ceil = Mathf.Min(CeilingAbove(from, 30f), Arena.yMax) - 1.2f;
                float reach = 40f;
                if (dir.y < -0.01f) reach = Mathf.Min(reach, (landY - from.y) / dir.y);
                if (dir.y > 0.01f) reach = Mathf.Min(reach, (ceil - from.y) / dir.y);
                if (Mathf.Abs(dir.x) > 0.01f) reach = Mathf.Min(reach, ((left ? wr : wl) - from.x) / dir.x);
                reach = Mathf.Max(2f, reach);
                Vector2 end = from + dir * reach;
                MarkLine(from, end, antic + 0.05f);
                Batch2.Play(go, "Dash Antic");
                yield return new WaitForSeconds(antic);
                sc = transform.localScale;
                bool right = dir.x >= 0f;
                transform.localScale = new Vector3(right ? Mathf.Abs(sc.x) : -Mathf.Abs(sc.x), sc.y, sc.z);
                transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(right ? Vector2.right : Vector2.left, dir));
                Batch2.Play(go, "Dash Attack");
                const float speed = 50f;
                float time = reach / speed;
                escort = Batch2.Escort(this, transform, Vector2.zero, 3f, time + 0.15f, Look.Blade);
                yield return Batch2.Travel(this, rb, end, speed, time + 0.15f);
                if (rb != null) rb.linearVelocity = Vector2.zero;
                Straighten();
                if (escort != null) Destroy(escort.gameObject);
                Batch2.Play(go, "Dash Recover");
                yield return new WaitForSeconds(0.2f);
            }
            finally
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                transform.rotation = rotation;
                if (escort != null) Destroy(escort.gameObject);
            }
        }
    }
}
