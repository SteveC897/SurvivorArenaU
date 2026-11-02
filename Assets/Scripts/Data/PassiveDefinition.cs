using System;
using UnityEngine;

namespace SurvivorArena.Data
{
    /// <summary>
    /// 一件被动道具的配置。
    /// LevelValues 存“该等级的累计修改器值”，例如菠菜 5 级是 0.10/0.20/0.30/0.40/0.50；
    /// 升级时用同一个 SourceId 调 SetModifierValue，避免留下多条同源修改器。
    /// </summary>
    [Serializable]
    public sealed class PassiveDefinition
    {
        public string Id;
        public string DisplayName;
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;
        public int MaxLevel = 5;
        public int Weight = 100;
        public PassiveEffect Effect;
        public PassiveModifierKind ModifierKind;
        [Tooltip("每个等级的累计修改器值，长度应等于 MaxLevel。")]
        public float[] LevelValues;

        [Tooltip("当前战斗范围是否把它放进升级池；false 表示定义保留但暂不提供。")]
        public bool Offered = true;

        public float GetValue(int level)
        {
            if (LevelValues == null || LevelValues.Length == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp(level - 1, 0, LevelValues.Length - 1);
            return LevelValues[index];
        }

        public bool IsMaxLevel(int level)
        {
            return level >= MaxLevel;
        }
    }
}
