using UnityEngine;

namespace TerraKnight.World
{
    internal sealed partial class Chests
    {
        /// <summary>Dev: the empty chest nearest a point (within 2 units) taken away, as a test put it down. True when one went.</summary>
        public static bool DevRemoveNear(Vector2 p)
        {
            int best = -1;
            float d = 2f;
            for (int i = 0; i < chests.Count; i++)
            {
                var c = chests[i];
                if (c.Region == null || !c.Empty) continue;
                float di = Vector2.Distance(c.Box.center, p);
                if (di < d) { d = di; best = i; }
            }
            if (best < 0) return false;
            Remove(best);
            return true;
        }
    }
}
