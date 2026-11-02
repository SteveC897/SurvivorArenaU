using System;

namespace SurvivorArena.Data
{
    /// <summary>
    /// 武器一级的完整数值。每把武器 8 级，升级时整行替换，不在运行时做增量累加。
    /// Area / Speed / Duration 是乘区（1 = 100%），Damage 是基础伤害，Amount 是基础数量。
    /// </summary>
    [Serializable]
    public struct WeaponLevelStats
    {
        public float Damage;
        public float Cooldown;
        public float Area;
        public float Speed;
        public float Duration;
        public int Amount;
        public int Pierce;
        public float Knockback;
        public float CritChance;
        public float CritMultiplier;

        public WeaponLevelStats(
            float damage,
            float cooldown,
            float area,
            float speed,
            float duration,
            int amount,
            int pierce,
            float knockback = 0f,
            float critChance = 0f,
            float critMultiplier = 1f)
        {
            Damage = damage;
            Cooldown = cooldown;
            Area = area;
            Speed = speed;
            Duration = duration;
            Amount = amount;
            Pierce = pierce;
            Knockback = knockback;
            CritChance = critChance;
            CritMultiplier = critMultiplier;
        }
    }
}
