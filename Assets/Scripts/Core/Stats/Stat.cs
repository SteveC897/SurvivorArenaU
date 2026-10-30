using System;
using System.Collections.Generic;

namespace SurvivorArena.Core.Stats
{
    /// <summary>
    /// 单条属性（Core.Stats）。
    ///
    /// 设计要点（对应《技术实现清单》M2）：
    ///   - 只读 Value：外部不能直接 +=，所有变化必须经过 AddModifier/RemoveModifier；
    ///   - 每次重算都从 BaseValue 开始，不做增量累加，所以移除修改器后不会残留；
    ///   - FlatAdd / PercentAdd / PercentMul 按固定顺序计算，和修改器注册顺序无关；
    ///   - 脏标记：同一帧多次 Add/Remove 只会在下一次读取 Value 时合并重算一次；
    ///   - Id 去重：同一条属性里相同 Id 的修改器会替换，不会重复叠加；
    ///   - SourceId 分组：RemoveAllModifiersFromSource 用于“移除某个被动/某张卡”的全部效果；
    ///   - SetModifierValue 用于震荡类效果（每帧只注入当前相位系数，不做乘算累积）。
    /// </summary>
    public sealed class Stat
    {
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private readonly float _minValue;

        private bool _dirty = true;
        private float _value;

        public Stat(StatType type, float baseValue, float minValue = float.NegativeInfinity)
        {
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), baseValue, "BaseValue 必须是有限数。");
            }

            if (float.IsNaN(minValue))
            {
                throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "MinValue 不能是 NaN。");
            }

            Type = type;
            BaseValue = baseValue;
            _minValue = minValue;
            _value = Clamp(baseValue);
        }

        public StatType Type { get; }

        /// <summary>基础值。修改基础值会触发下一次读取时重算。</summary>
        public float BaseValue { get; private set; }

        /// <summary>最终值。读取时如果脏了就重算一次。</summary>
        public float Value
        {
            get
            {
                RecalculateIfNeeded();
                return _value;
            }
        }

        /// <summary>当前所有修改器（只读视图，按注册顺序）。</summary>
        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        /// <summary>累计重算次数，仅用于调试/测试脏标记是否生效。</summary>
        public int RecalculationCount { get; private set; }

        /// <summary>重算后最终值发生变化时触发；UI 可订阅它刷新数字。</summary>
        public event Action<Stat> Changed;

        public void SetBaseValue(float baseValue)
        {
            if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), baseValue, "BaseValue 必须是有限数。");
            }

            if (BaseValue.Equals(baseValue))
            {
                return;
            }

            BaseValue = baseValue;
            MarkDirty();
        }

        /// <summary>
        /// 添加或替换一条修改器。
        /// 同一个 Id 已存在时直接替换（最后一次写入生效），不会叠成两条。
        /// </summary>
        public void AddModifier(StatModifier modifier)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (string.Equals(_modifiers[i].Id, modifier.Id, StringComparison.Ordinal))
                {
                    _modifiers[i] = modifier;
                    MarkDirty();
                    return;
                }
            }

            _modifiers.Add(modifier);
            MarkDirty();
        }

        /// <summary>按 Id 精确移除一条修改器；返回是否找到。</summary>
        public bool RemoveModifier(string id)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (string.Equals(_modifiers[i].Id, id, StringComparison.Ordinal))
                {
                    _modifiers.RemoveAt(i);
                    MarkDirty();
                    return true;
                }
            }

            return false;
        }

        /// <summary>移除某个来源注册的全部修改器；返回移除数量。</summary>
        public int RemoveAllModifiersFromSource(string sourceId)
        {
            int removed = 0;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_modifiers[i].SourceId, sourceId, StringComparison.Ordinal))
                {
                    _modifiers.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                MarkDirty();
            }

            return removed;
        }

        /// <summary>
        /// 修改一条已存在修改器的数值（用于震荡/蓄力等动态效果），并标脏。
        /// 找不到对应 Id 时返回 false。
        /// </summary>
        public bool SetModifierValue(string id, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "修改器数值必须是有限数。");
            }

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier modifier = _modifiers[i];
                if (!string.Equals(modifier.Id, id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (modifier.Value.Equals(value))
                {
                    return true;
                }

                _modifiers[i] = new StatModifier(modifier.SourceId, modifier.Type, value, modifier.Id);
                MarkDirty();
                return true;
            }

            return false;
        }

        /// <summary>清空全部修改器（通常用于一局结束/对象复用）。</summary>
        public void ClearModifiers()
        {
            if (_modifiers.Count == 0)
            {
                return;
            }

            _modifiers.Clear();
            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void RecalculateIfNeeded()
        {
            if (!_dirty)
            {
                return;
            }

            float flat = 0f;
            float percentAdd = 0f;
            float percentMul = 1f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier modifier = _modifiers[i];
                switch (modifier.Type)
                {
                    case StatModifierType.FlatAdd:
                        flat += modifier.Value;
                        break;
                    case StatModifierType.PercentAdd:
                        percentAdd += modifier.Value;
                        break;
                    case StatModifierType.PercentMul:
                        percentMul *= 1f + modifier.Value;
                        break;
                }
            }

            float oldValue = _value;
            float rawValue = (BaseValue + flat) * (1f + percentAdd) * percentMul;
            _value = Clamp(rawValue);
            _dirty = false;
            RecalculationCount++;

            if (!oldValue.Equals(_value))
            {
                Changed?.Invoke(this);
            }
        }

        private float Clamp(float value)
        {
            return value < _minValue ? _minValue : value;
        }
    }
}
