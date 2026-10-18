using UnityEngine;

namespace TerraKnight.Items
{
    internal sealed partial class Minions
    {
        /// <summary>Dev: whether the minions would chase an enemy now, and if not, which of FindNpcs' rules leaves it out.</summary>
        public string DevWhyNot(HealthManager hm, Kind kind)
        {
            if (hm == null) return "没有";
            var info = EnemyStats.Of(hm);
            string rules = $"无敌 {hm.IsInvincible}/{hm.InvincibleFromDirection} 碰不到 {EnemyStats.Untouchable(hm)} 睡 {BossWake.Asleep(hm)} 生命上限 {(info != null ? EnemyStats.LifeMax(info) : -1)} 门外 {World.ArenaGuard.Outside(hm)} 地图外 {World.ArenaGuard.OffMap(hm)}";
            var main = hm.GetComponent<Collider2D>();
            if (main != null) rules += $" 埋着 {Buried(main.bounds)} 主碰撞 {main.enabled} {main.GetType().Name} 层 {main.gameObject.layer}";
            if (!npcById.TryGetValue(hm.GetInstanceID(), out var n)) return "不在名单 " + rules;
            foreach (var m in live)
            {
                if (m.Dead || m.Kind != kind) continue;
                bool reach = CanReach(m, n);
                bool range = InRangeOfMeOrOwner(m, n, 800f, out float my, out float pl);
                return $"在名单 能到 {reach}（它看得见 {Sees(m.Center, n)} 主角看得见 {Sees(pCenter, n)}）范围内 {range}（离它 {my:F0} 离主角 {pl:F0}）目标 {(FindTargetInRange(m, 800f, false) == n)} " + rules;
            }
            return "在名单，没有这种召唤物 " + rules;
        }
    }
}
