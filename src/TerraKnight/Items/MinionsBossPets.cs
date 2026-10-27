using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class Minions
    {
        /// <summary>
        /// A boss pet's frames by its sheet (Items.BossPets), after its move: on the ground idle 0-3 (10 ticks each) while
        /// still, facing the player, the walk 4-11 (quicker the faster), up 12 and down 13 in the air, 14-19 (4 ticks) flying
        /// after the player (AI_026's flight), leant into it; by the shoulder the float 0-7 (5 ticks) and now and then the
        /// gesture 8-11 (6 ticks). A flyer's loop is PetFlyFrames'.
        /// </summary>
        void BossPetFrames(M m, PetProj p)
        {
            switch (p.Boss)
            {
                case BossPets.Kind.Ground:
                    if (m.Ai0 != 0f)
                    {
                        LoopPet(m, 14, 20, 14, 4);
                        m.Rot = m.Vel.x * 0.04f;
                        return;
                    }
                    m.Rot = 0f;
                    if (m.Vel.y != 0f)
                    {
                        m.Frame = m.Vel.y < 0f ? 12 : 13;
                        m.FrameCounter = 0;
                        return;
                    }
                    if (Mathf.Abs(m.Vel.x) < 0.3f)
                    {
                        LoopPet(m, 0, 4, 0, 10);
                        // Its art faces left: turned to look at the player.
                        m.SpriteDir = pCenter.x > m.Center.x ? -1 : 1;
                        return;
                    }
                    LoopPet(m, 4, 12, 4, Mathf.Clamp(Mathf.RoundToInt(6f - Mathf.Abs(m.Vel.x) * 0.5f), 2, 5));
                    return;
                case BossPets.Kind.Anchor:
                    if (m.Frame >= 8)
                    {
                        if (++m.FrameCounter < 6) return;
                        m.FrameCounter = 0;
                        if (++m.Frame >= 12) m.Frame = 0;
                        return;
                    }
                    LoopPet(m, 0, 8, 0, 5);
                    // At the float's start, every three to six floats (two to five seconds), the gesture.
                    if (m.Frame == 0 && m.FrameCounter == 0 && --m.LocalAi2 <= 0f)
                    {
                        m.Frame = 8;
                        m.LocalAi2 = Random.Range(3, 7);
                    }
                    return;
            }
        }
    }
}
