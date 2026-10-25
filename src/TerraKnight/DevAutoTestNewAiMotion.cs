using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// What a watched enemy's body did that it shouldn't: jumps faster than any of its moves goes (a teleport) while it
        /// is in sight, its middle and upper half in the room's own rock (stuck in a wall), the longest it held one state of
        /// a move, the longest it stood without moving at all.
        /// </summary>
        sealed class AiMotion
        {
            public int Jumps;
            public float WorstJump, WallMost, StateMost, StillMost;
            public string WorstAt = "", StateAt = "";
            float wall, still, inState;
            string state;
            Vector2 last;
            bool first = true;

            public void Step(Vector2 pos, Collider2D body, string st, bool mayVanish, float speedLimit, bool countState)
            {
                float dt = Time.deltaTime;
                if (!first)
                {
                    float step = (pos - last).magnitude;
                    if (!mayVanish && step > speedLimit * Mathf.Max(dt, Time.fixedDeltaTime) + 0.6f)
                    {
                        Jumps++;
                        if (step > WorstJump) { WorstJump = step; WorstAt = st; }
                    }
                    if (step < 0.02f) { still += dt; StillMost = Mathf.Max(StillMost, still); }
                    else still = 0f;
                }
                first = false;
                last = pos;
                if (body != null && body.enabled && !mayVanish)
                {
                    var b = body.bounds;
                    bool stone = EnemyAi.Hostiles.NativeSolidAt(b.center) && EnemyAi.Hostiles.NativeSolidAt(new Vector2(b.center.x, b.center.y + b.extents.y * 0.5f));
                    if (stone) { wall += dt; WallMost = Mathf.Max(WallMost, wall); }
                    else wall = 0f;
                }
                else wall = 0f;
                if (countState && st == state) { inState += dt; if (inState > StateMost) { StateMost = inState; StateAt = st; } }
                else { state = st; inState = 0f; }
            }

            public string Report => $"jumps={Jumps} worstJump={WorstJump:F1}@{WorstAt} inWall={WallMost:F1} longestState={StateMost:F1}@{StateAt} stillMost={StillMost:F1}";
        }
    }
}
