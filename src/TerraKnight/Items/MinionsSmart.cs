using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Chester in SmartInteract (Terraria's ProjectileSmartInteractCandidateProvider: Projectile.IsInteractable's pets and
    /// banks, here only Chester): within reach, weighed by his box's distance from the mouse; picked, a right click
    /// anywhere opens or shuts the bank on him.
    /// </summary>
    internal sealed partial class Minions
    {
        public static bool SmartChester(Vector2 mouseWorld, out float distance)
        {
            distance = float.MaxValue;
            return Instance != null && Instance.ChesterDistance(TerraParticles.ToPx(mouseWorld), out distance);
        }

        bool ChesterDistance(Vector2 mousePx, out float distance)
        {
            distance = float.MaxValue;
            RefreshOwner();
            var m = Chester;
            if (!hasOwner || m == null || ownerDead || !ChesterInReach(m)) return false;
            var b = m.Box;
            float dx = Mathf.Max(b.xMin - mousePx.x, 0f, mousePx.x - b.xMax), dy = Mathf.Max(b.yMin - mousePx.y, 0f, mousePx.y - b.yMax);
            distance = Mathf.Sqrt(dx * dx + dy * dy) * TerraParticles.Px;
            return true;
        }

        /// <summary>A right click anywhere with Chester picked: as a click on his middle.</summary>
        public static bool TrySmartChester()
        {
            if (Instance == null) return false;
            Instance.RefreshOwner();
            var m = Instance.Chester;
            return m != null && Instance.ChesterClick(m.Box.center);
        }
    }
}
