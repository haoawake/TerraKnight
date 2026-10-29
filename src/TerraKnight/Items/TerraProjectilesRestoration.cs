using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        static void RegisterRestorationSpray()
        {
            specs[ProjKind.RestorationSpray] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_145", W = 8, H = 8,
                Time = 60, Penetrate = -1, TileCollide = false, Look = Look.Additive,
                Light = new Color(.15f, .6f, .25f), CanDamage = p => false,
                Update = (t, p) =>
                {
                    var cell = CellGrid.WorldToCell(p.Pos);
                    if (p.TimeLeft >= 59 || p.Ai0 != cell.x || p.Ai1 != cell.y)
                    {
                        RestorationGun.Spray(p.Pos);
                        p.Ai0 = cell.x; p.Ai1 = cell.y;
                    }
                    p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
                    int dust = PointDust(CenterPx(p), 110, p.Vel * .15f, 0, 1.3f);
                    if (dust >= 0) { D(dust).NoGravity = true; D(dust).Color = new Color(.3f, 1f, .55f); }
                },
                Draw = (t, fx, p, px) => DrawGeneric(fx, p, px, new Color(.3f, 1f, .55f, .8f)),
            };
        }
    }
}
