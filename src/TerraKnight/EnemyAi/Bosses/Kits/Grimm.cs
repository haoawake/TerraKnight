using System.Collections;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Troupe Master's and the Nightmare King's kits in common ("Control"; 空洞骑士首领新版AI设计.md §2.7):
    /// <list type="bullet">
    /// <item>the firebats he casts fly at where the player is going (each one's way drawn as it goes: the first of each
    /// pair carried up or down to it);</item>
    /// <item>the dive from the air: sometimes led, its line drawn down to the floor while he winds up; the dash along the
    /// floor after it drawn the same;</item>
    /// <item>the cape spikes: one more of his own rises where the player stands (marked first);</item>
    /// <item>the uppercut's fireballs lean towards the player, the middle one's landing marked; the uppercut's way up drawn
    /// as he crouches;</item>
    /// <item>against the air: he comes out beside the player and goes straight into the uppercut (the slash's way in),
    /// which rises as high as the player is (his uppercut's own top is a set height);</item>
    /// <item>a player well up (on a platform, hanging under the ceiling) gets his slash at his level: he comes out at that
    /// height and lunges across along it;</item>
    /// <item>a quicker pace between his moves (×0.85). His balloon, his bows and his reforming are the game's own.</item>
    /// </list>
    /// </summary>
    internal abstract class GrimmFamilyKit : BossKit
    {
        protected override float Pace => 0.85f;
        protected override Look Theme => Look.Fire;
        /// <summary>Hidden between his moves where his teleports leave him (the uppercut's top, the balloon's place).</summary>
        protected override bool Roams => true;

        /// <summary>The states that cast a firebat (their $Projectile the first of each pair).</summary>
        protected abstract string[] Bats { get; }

        bool forceUppercut;
        int retries;

        /// <summary>The third look in a row with no spot: Tele X the nearest spot within his range, 8 to the player's roomier side.</summary>
        bool GiveUpLooking(string minName, string maxName)
        {
            if (++retries < 3) return false;
            var tele = Float("Tele X");
            if (tele == null) return false;
            retries = 0;
            float min = Float(minName)?.Value ?? Arena.xMin, max = Float(maxName)?.Value ?? Arena.xMax;
            float hx = TargetCenter.x;
            float left = Mathf.Clamp(hx - 8f, min, max), right = Mathf.Clamp(hx + 8f, min, max);
            tele.Value = Mathf.Abs(left - hx) >= Mathf.Abs(right - hx) ? left : right;
            return true;
        }
        /// <summary>His Ground Y while a slash comes at a player's level; NaN otherwise.</summary>
        float savedGround = float.NaN;
        /// <summary>The uppercut's own top (where it stops and where it bursts), its rise's longest time; raised for one uppercut.</summary>
        FloatCompare upTop;
        Wait upTime;
        SetPosition upBurst;
        float upTopBase, upTimeBase, upBurstBase;
        bool upRaised;

        protected override void Setup()
        {
            Tempo("Out Pause", "Explode Pause", "FB Cast End", "G Dash Recover");
            At("AD Antic", 1, LeadDive);
            OnEnter("AD Antic", MarkDive, first: false);
            OnEnter("GD Antic", MarkFloorDash, first: false);
            OnEnter("Spike Attack", LockedSpike);
            OnEnter("Uppercut Antic", () => MarkLine(Me, Me + Vector2.up * 9f, Warn(0.45f)));
            AddState("TK Rise Pos", RisePos, "Slash Tele In");
            Branch("Move Choice", "TK Rise Pos", WantAntiAir);
            Branch("Uppercut?", "Uppercut Antic", () =>
            {
                if (!forceUppercut) return false;
                forceUppercut = false;
                return true;
            });
            // Out at the player's level, the slash itself (his Uppercut Range can be left over from an earlier way in).
            if (Batch2.Has(Control, "Auto Evade?")) Branch("Uppercut?", "Auto Evade?", () => !float.IsNaN(savedGround) && !forceUppercut);
            OnEnter("Slash Pos", LevelSlash);
            // His ways in look for a spot beside the player and look again the next frame when there is none (a player at
            // the room's side, on a ledge past where he comes out): after a few, the nearest spot there is.
            foreach (var (retry, tele, min, max) in new[]
            {
                ("AD Retry", "AD Tele In", "AD Min X", "AD Max X"),
                ("Slash Retry", "Slash Tele In", "Min X", "Max X"),
                ("Spike Retry", "Spike Tele In", "Min X", "Max X"),
            })
            {
                if (!Batch2.Has(Control, retry) || !Batch2.Has(Control, tele)) continue;
                string lo = min, hi = max;
                Branch(retry, tele, () => GiveUpLooking(lo, hi));
                OnEnter(tele, () => retries = 0);
            }
            OnEnter("Tele Out", Unlevel);
            OnEnter("Move Choice", Unlevel);
            upTop = Batch1Helpers.First<FloatCompare>(State("Uppercut Up"));
            upTime = Batch1Helpers.First<Wait>(State("Uppercut Up"));
            upBurst = Batch1Helpers.First<SetPosition>(State("UP Explode"));
            if (upTop != null && upTime != null && upBurst != null && upBurst.y != null)
            {
                upTopBase = upTop.float2.Value;
                upTimeBase = upTime.time.Value;
                upBurstBase = upBurst.y.Value;
                OnEnter("Uppercut Up", ReachUp);
                OnEnter("Explode Pause", LowerUp);
                OnEnter("Move Choice", LowerUp);
            }
            SetupMore();
        }

        /// <summary>
        /// A player well up off the floor: the slash comes at his level most of the time (his Ground Y, where he comes out
        /// for it, taken up to the player's feet; the lunge goes across along it).
        /// </summary>
        void LevelSlash()
        {
            Unlevel();
            var g = Float("Ground Y");
            if (g == null || !HasTarget || Grace) return;
            if (Random.value >= 0.75f) return;
            LevelSlashForced(g);
        }

        void LevelSlashForced(HutongGames.PlayMaker.FsmFloat g)
        {
            Unlevel();
            if (!HasTarget) return;
            float floor = GroundBelow(new Vector2(TargetFeet.x, g.Value + 0.5f));
            if (TargetFeet.y - floor < 2.5f) return;
            savedGround = g.Value;
            float ceil = CeilingAbove(TargetCenter, 30f);
            g.Value = Mathf.Min(TargetFeet.y + (savedGround - floor), ceil - 1.5f);
            BossAi.DevAntiAir++;
        }

        void Unlevel()
        {
            if (float.IsNaN(savedGround)) return;
            var g = Float("Ground Y");
            if (g != null) g.Value = savedGround;
            savedGround = float.NaN;
        }

        /// <summary>The uppercut up as high as the player is (under the ceiling), when he is over its own top.</summary>
        void ReachUp()
        {
            LowerUp();
            if (!HasTarget) return;
            float want = Mathf.Min(TargetCenter.y + 1f, CeilingAbove(Me, 40f) - 1.5f);
            if (want <= upTopBase) return;
            upRaised = true;
            upTop.float2.Value = want;
            upBurst.y.Value = want;
            upTime.time.Value = Mathf.Max(upTimeBase, (want - Me.y) / 45f + 0.05f);
        }

        void LowerUp()
        {
            if (!upRaised) return;
            upRaised = false;
            upTop.float2.Value = upTopBase;
            upBurst.y.Value = upBurstBase;
            upTime.time.Value = upTimeBase;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Unlevel();
            if (upTop != null) LowerUp();
        }

        /// <summary>The Nightmare King's own on top.</summary>
        protected virtual void SetupMore() { }


        /// <summary>The dive's angle (degrees, 270 straight down) at where the player will be, now and then.</summary>
        void LeadDive()
        {
            var angle = Float("Angle");
            if (angle == null || !RollLead()) return;
            var d = Lead(0.35f) + Vector2.down * 0.5f - Me;
            float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            angle.Value = a < 0f ? a + 360f : a;
        }

        /// <summary>The dive's way down to the floor, drawn while he winds up.</summary>
        void MarkDive()
        {
            var angle = Float("Angle");
            if (angle == null) return;
            float a = Mathf.Clamp(angle.Value, 200f, 340f) * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float floor = Float("Ground Y")?.Value ?? GroundBelow(Me);
            float len = d.y < -0.05f ? (Me.y - floor) / -d.y : 12f;
            MarkLine(Me, Me + d * Mathf.Min(len, 30f), Warn(0.45f));
        }

        /// <summary>The dash along the floor after the dive, drawn while he crouches (he faces the player; his sprite faces left).</summary>
        void MarkFloorDash()
        {
            float dir = transform.localScale.x > 0f ? -1f : 1f;
            float wall = WallTowards(Me, dir, 30f);
            float end = Mathf.Abs(wall - Me.x) < 16f ? wall - dir * 0.5f : Me.x + dir * 16f;
            MarkLine(Me, new Vector2(end, Me.y), Warn(0.5f));
        }

        /// <summary>
        /// One more spike of his own where the player stands as the cape's spikes rise: a copy of one of the room's spikes,
        /// peeking, rising and going down as the room's do (their own pictures and harm).
        /// </summary>
        void LockedSpike()
        {
            if (Grace || !RoomForStrike) return;
            var at = OnGround(Lock());
            if (spikeModel == null)
            {
                var list = Batch3.SceneFsms(gameObject.scene, "Grimm Spike", "Control");
                if (list.Count == 0) list = Batch3.SceneFsms(gameObject.scene, "Nightmare Spike", "Control");
                spikeModel = list.Count > 0 ? list[0].gameObject : null;
            }
            if (spikeModel != null) Run(OwnSpike(at));
        }

        GameObject spikeModel;

        IEnumerator OwnSpike(Vector2 at)
        {
            // Beside the room's own, under their holder (Godhome's spikes set their height in it).
            var copy = Instantiate(spikeModel);
            copy.transform.SetParent(spikeModel.transform.parent, true);
            copy.transform.position = new Vector3(at.x, spikeModel.transform.position.y, spikeModel.transform.position.z);
            PlayMakerFSM fsm = null;
            foreach (var f in copy.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Control") fsm = f;
            // Its own start (a frame or two) to the dormant spike, then the room's call: ready, up, down.
            yield return null;
            yield return null;
            if (fsm == null) { Destroy(copy); yield break; }
            fsm.SendEvent("SPIKES READY");
            yield return new WaitForSeconds(Warn(0.55f));
            fsm.SendEvent("SPIKES UP");
            BossAi.DevMoves++;
            yield return new WaitForSeconds(0.6f);
            if (copy == null) yield break;
            fsm.SendEvent("SPIKES DOWN");
            yield return new WaitForSeconds(0.6f);
            if (copy != null) Destroy(copy);
        }


        /// <summary>Against the air: in beside the player (his slash's way in), the uppercut next whatever.</summary>
        IEnumerator RisePos()
        {
            var tele = Float("Tele X");
            float min = Float("Min X")?.Value ?? Arena.xMin, max = Float("Max X")?.Value ?? Arena.xMax;
            float side = Random.value < 0.5f ? -1f : 1f;
            // Half the time, a player well up gets the slash at his level instead (out at his height, across at him).
            var g = Float("Ground Y");
            if (g != null && Random.value < 0.5f)
            {
                LevelSlashForced(g);
                if (!float.IsNaN(savedGround))
                {
                    if (tele != null) tele.Value = Mathf.Clamp(TargetCenter.x + side * Random.Range(7.5f, 9f), min, max);
                    yield break;
                }
            }
            float x = Mathf.Clamp(TargetCenter.x + side * 1.5f, min, max);
            if (tele != null) tele.Value = x;
            forceUppercut = true;
            MarkGround(new Vector2(x, TargetFeet.y), 4f, Warn(0.6f));
            yield break;
        }
    }

    /// <summary>The Troupe Master Grimm (the tent's, Godhome's): <see cref="GrimmFamilyKit"/>.</summary>
    [Kit("Grimm")]
    internal sealed class GrimmKit : GrimmFamilyKit
    {
        protected override string[] Bats => new[] { "Firebat 1", "Firebat 2", "Firebat 3" };
    }
}
