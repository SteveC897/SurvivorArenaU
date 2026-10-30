using System;
using SurvivorArena.Data;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 波次时间轴（Gameplay 层）。
    /// 只做一件事：根据“局内已过时间”扫描 WaveTable，到点就触发 WaveTriggered。
    /// 不读 Time、不发 UI 事件、不刷怪——时间由 BattleSession 用 GameClock 喂进来。
    /// </summary>
    public sealed class WaveScheduler
    {
        private readonly WaveTable _table;
        private int _nextIndex;

        public WaveScheduler(WaveTable table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        public event Action<WaveDefinition> WaveTriggered;

        public int TotalWaves => _table.Count;

        public int NextIndex => _nextIndex;

        public WaveTable Table => _table;

        public void Reset()
        {
            _nextIndex = 0;
        }

        /// <summary>把时间轴推进到 elapsed 秒；可能一次触发多波（帧率抖动时）。</summary>
        public void Advance(float elapsed)
        {
            while (_nextIndex < _table.Count && elapsed >= _table[_nextIndex].Time)
            {
                WaveDefinition wave = _table[_nextIndex];
                _nextIndex++;
                WaveTriggered?.Invoke(wave);
            }
        }
    }
}
