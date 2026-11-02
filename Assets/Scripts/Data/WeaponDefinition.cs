using System;
using UnityEngine;

namespace SurvivorArena.Data
{
    /// <summary>
    /// 一把武器的只读配置。武器 = 数据（本类）+ 行为枚举，运行期由集中式 WeaponSystem 调度。
    /// </summary>
    [Serializable]
    public sealed class WeaponDefinition
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public Sprite ProjectileSprite;
        public WeaponBehavior Behavior;
        public int MaxLevel = 8;
        public int Weight = 100;

        [Tooltip("8 级完整数值；下标 0 = Lv1。")]
        public WeaponLevelStats[] Levels;

        public WeaponLevelStats GetLevelStats(int level)
        {
            if (Levels == null || Levels.Length == 0)
            {
                return default;
            }

            int index = Mathf.Clamp(level - 1, 0, Levels.Length - 1);
            return Levels[index];
        }

        public bool IsMaxLevel(int level)
        {
            return level >= MaxLevel;
        }
    }
}
