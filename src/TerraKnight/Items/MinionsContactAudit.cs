using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class Minions
    {
        internal void DevCatContactAudit(HealthManager target)
        {
            if (!DevCopy.Verified || target == null) throw new System.InvalidOperationException("isolated dummy required");
            bool kept = MinionEngage.DevAlways;
            MinionEngage.DevAlways = true;
            try
            {
                foreach (var kind in new[] { Kind.Cattiva, Kind.TrustyCattiva })
                {
                    var m = new M { Kind = kind, S = specs[kind], W = 14, H = 22, Damage = 10, OriginalDamage = 10,
                        Friendly = true, Ai0 = 2, Ai1 = 16, Dir = 1, SpriteDir = 1 };
                    var center = target.GetComponent<Collider2D>().bounds.center;
                    m.Center = Effects.TerraParticles.ToPx(center) - new Vector2(8, 0);
                    Physics2D.SyncTransforms();
                    int before = TargetDummy.DevHits;
                    var accepted = new List<int>();
                    for (int tick = 0; tick < 60; tick++)
                    {
                        var keys = new List<int>(m.Immune.Keys);
                        foreach (int key in keys) if (m.Immune[key] > 0 && --m.Immune[key] <= 0) m.Immune.Remove(key);
                        int prior = TargetDummy.DevHits;
                        // Repeated collision queries and overlapping hurtboxes in the same tick must share immunity.
                        Damage(m); Damage(m); Damage(m);
                        if (TargetDummy.DevHits > prior) accepted.Add(tick);
                    }
                    bool pass = accepted.Count == 7;
                    for (int i = 1; i < accepted.Count; i++) pass &= accepted[i] - accepted[i - 1] >= 9;
                    Plugin.Log.LogInfo($"[AutoTest] 捣蛋猫 {kind} 60 tick 贴身实际命中 {TargetDummy.DevHits - before}，帧 {string.Join(",", accepted)} {(pass ? "通过" : "不通过")}");
                    m.Immune.Clear(); m.Ai0 = 0; m.Ai1 = 0;
                    before = TargetDummy.DevHits;
                    Damage(m);
                    pass = before == TargetDummy.DevHits;
                    Plugin.Log.LogInfo($"[AutoTest] 捣蛋猫 {kind} 停手贴身不造成伤害 {(pass ? "通过" : "不通过")}");
                    // Body behind the fist may overlap a target; only the forward punch box may strike.
                    m.Ai0 = 2; m.Ai1 = 16; m.Center = Effects.TerraParticles.ToPx(center) + new Vector2(18, 0);
                    m.W = 40;
                    before = TargetDummy.DevHits;
                    Damage(m);
                    pass = before == TargetDummy.DevHits;
                    Plugin.Log.LogInfo($"[AutoTest] 捣蛋猫 {kind} 身体贴住但拳头未碰到不伤害 {(pass ? "通过" : "不通过")}");
                }
            }
            finally { MinionEngage.DevAlways = kept; }
        }
    }
}
