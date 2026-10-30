using System;
using System.Collections.Generic;

namespace SurvivorArena.Core.Stats
{
    /// <summary>
    /// 一组属性的容器（Core.Stats）。
    /// 典型用法：PlayerStats 构建玩家的全部属性，敌人/召唤物也可以各自持有一个 StatBlock。
    /// </summary>
    public sealed class StatBlock
    {
        private readonly Dictionary<StatType, Stat> _stats = new Dictionary<StatType, Stat>();

        public StatBlock(IEnumerable<Stat> stats)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            foreach (Stat stat in stats)
            {
                if (stat == null)
                {
                    throw new ArgumentException("StatBlock 不能包含 null。", nameof(stats));
                }

                if (_stats.ContainsKey(stat.Type))
                {
                    throw new ArgumentException($"StatBlock 中重复的 StatType：{stat.Type}。", nameof(stats));
                }

                _stats.Add(stat.Type, stat);
            }
        }

        public int Count => _stats.Count;

        public Stat Get(StatType type)
        {
            if (_stats.TryGetValue(type, out Stat stat))
            {
                return stat;
            }

            throw new KeyNotFoundException($"StatBlock 不存在属性：{type}。");
        }

        public bool TryGet(StatType type, out Stat stat)
        {
            return _stats.TryGetValue(type, out stat);
        }
    }
}
