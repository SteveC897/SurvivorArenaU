using System;

namespace SurvivorArena.Core.Stats
{
    /// <summary>
    /// 一条属性修改器（不可变值类型）。
    ///
    /// SourceId：谁提供的（例如 "passive.hollowHeart" / "arcana.XVI"），用于按来源整组移除。
    /// Id：同一条属性内的唯一键，用于去重/替换/精确移除。
    ///     一个来源只加一条时可以省略 Id（自动等于 SourceId）；
    ///     一个来源要给同一条属性加多条时，必须给不同的 Id，
    ///     例如 SourceId = "passive.hollowHeart"，Id = "passive.hollowHeart.flat" / "...percent"。
    /// </summary>
    public readonly struct StatModifier
    {
        public readonly string SourceId;
        public readonly string Id;
        public readonly StatModifierType Type;
        public readonly float Value;

        public StatModifier(string sourceId, StatModifierType type, float value, string id = null)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("修改器必须有 SourceId。", nameof(sourceId));
            }

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "修改器数值必须是有限数。");
            }

            SourceId = sourceId;
            Id = string.IsNullOrWhiteSpace(id) ? sourceId : id;
            Type = type;
            Value = value;
        }

        public static StatModifier Flat(string sourceId, float value, string id = null)
        {
            return new StatModifier(sourceId, StatModifierType.FlatAdd, value, id);
        }

        public static StatModifier PercentAdd(string sourceId, float value, string id = null)
        {
            return new StatModifier(sourceId, StatModifierType.PercentAdd, value, id);
        }

        public static StatModifier PercentMul(string sourceId, float value, string id = null)
        {
            return new StatModifier(sourceId, StatModifierType.PercentMul, value, id);
        }

        public override string ToString()
        {
            return $"{SourceId}/{Id} {Type} {Value:0.###}";
        }
    }
}
