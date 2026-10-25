using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class WorldItems
    {
        public static int DevShimmered;
        const int PopTicks = 24;

        /// <summary>
        /// WorldItem.UpdateShimmer, once a tick; true while gravity is off for it. What came out of the shimmer floats: it
        /// rises in it (0.05 px/tick² up to 4), keeps rising just over it, and slows to a stop above it. An item lying in it
        /// that can change (Shimmer.CanShimmer) changes once it has been in for 90 ticks (shimmerTime 0.01 a tick, at 0.9);
        /// out of it the time runs back down. A remote physics owner requests the
        /// canonical conversion from the host; it never creates its own outputs.
        /// </summary>
        static bool ShimmerTick(Item it)
        {
            var at = it.World;
            bool wet = Shimmer.In(at);
            if (it.Pop > 0) it.Pop--;
            if (it.Shimmered)
            {
                if (Random.Range(0, 12) == 0)
                    Shimmer.Sparkle(Effects.TerraParticles.ToPx(at) - new Vector2(12f, 12f), 20, 20, 1.1f, 0.3f);
                if (wet || Shimmer.JustAbove(at))
                {
                    if (it.Vel.y < 4f) it.Vel.y += 0.05f;
                }
                else it.Vel.y *= 0.9f;
            }
            if (wet && !it.Shimmered && Shimmer.CanShimmer(it.Stack))
            {
                it.ShimmerTime = Mathf.Min(1f, it.ShimmerTime + 0.01f);
                if (it.ShimmerTime > 0.9f)
                {
                    it.ShimmerTime = 0.9f;
                    if (it.NetId == null || Net.NetSession.Hosting) Shimmered(it);
                    else if (Time.unscaledTime - it.ShimmerRequestAt >= 1f)
                    {
                        it.ShimmerRequestAt = Time.unscaledTime;
                        Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "shimmer", Key = it.NetId });
                    }
                }
            }
            else if (it.ShimmerTime > 0f) it.ShimmerTime = Mathf.Max(0f, it.ShimmerTime - 0.01f);
            return it.Shimmered;
        }

        /// <summary>
        /// WorldItem.GetShimmered for what Hallownest has: the whole stack turns into its transform (a fresh item of it, the
        /// same count); else as many whole batches of its recipe as are there come apart into what that recipe takes (or its
        /// customShimmerResults), each piece out a little to the side (SpawnShimmeredItem), an alchemy recipe's losing each unit
        /// at one in three; what is left over stays. Item_176 and twenty sparkles (ShimmerEffect).
        /// </summary>
        static void Shimmered(Item it, bool award = true)
        {
            int shimmeredCount = it.Stack.Count;
            var at = it.World;
            var to = Shimmer.TransformOf(it.Stack.Type, out _);
            if (to != ItemType.None)
            {
                it.Stack = ItemStack.Of(to, it.Stack.Count);
                it.Shimmered = true;
                it.Pop = PopTicks;
            }
            else
            {
                var r = Shimmer.DecraftRecipe(it.Stack.Type, Shop.UnlockedStage());
                if (r == null) return;
                int batches = it.Stack.Count / Mathf.Max(1, r.Count);
                bool alchemy = Shimmer.Alchemy(r);
                int k = 0;
                foreach (var (type, count) in Shimmer.Results(r))
                {
                    int n = batches * count;
                    if (alchemy)
                        for (int u = n; u > 0; u--)
                            if (Random.Range(0, 3) == 0) n--;
                    int max = Mathf.Max(1, ItemDb.Get(type)?.MaxStack ?? 1);
                    while (n > 0)
                    {
                        int part = Mathf.Min(n, max);
                        n -= part;
                        var velocity = new Vector2(k * (k % 2 * 2 - 1), 0f);
                        Item made;
                        if (it.NetId != null)
                        {
                            // The host may not have this scene loaded. Preserve
                            // scene-local coordinates instead of using its hero's region.
                            made = new Item { NetId = "i:" + System.Guid.NewGuid().ToString("N"),
                                Stack = ItemStack.Of(type, part), Scene = it.Scene, Local = it.Local,
                                Region = it.Region, Vel = velocity, Dropper = -1 };
                            items.Add(made);
                        }
                        else made = Spawn(ItemStack.Of(type, part), at, velocity, 0, personal: true);
                        k++;
                        if (made == null) continue;
                        made.Shimmered = true;
                        made.ShimmerTime = 1f;
                        made.Pop = PopTicks;
                    }
                }
                it.Stack.Count -= batches * r.Count;
                if (it.Stack.Count <= 0) it.Stack = default;
            }
            it.ShimmerTime = it.Stack.IsEmpty ? 0f : 1f;
            it.Vel *= 0.1f;
            DevShimmered++;
            if (award) Achievements.AchievementTracker.Shimmered(shimmeredCount);
            TerraMod.Instance?.PlaySound("Item_176", 0.8f);
            // ShimmerEffect's twenty sparkles, here thrown wider and joined by a splash off the surface, so the change shows.
            var px = Effects.TerraParticles.ToPx(at);
            for (int i = 0; i < 30; i++) Shimmer.Sparkle(px - new Vector2(6f, 6f), 12, 12, 1.3f, 1.8f);
            var pp = Effects.TerraParticles.Instance;
            if (pp != null)
                for (int i = 0; i < 10; i++)
                {
                    int d = pp.NewDust(px - new Vector2(10f, 4f), 20, 8, 308, 0f, -2f);
                    pp[d].Color = Shimmer.SparkleColor();
                    pp[d].Scale *= 1.2f;
                }
        }
    }
}
