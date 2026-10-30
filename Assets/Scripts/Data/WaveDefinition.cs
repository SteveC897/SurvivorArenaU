using System;

namespace SurvivorArena.Data
{
    /// <summary>
    /// 单波配置（Data 层）。数据源：Assets/DataDocs/level01_enemy_spawn.csv。
    /// 测试场景只使用数量下限与补刷间隔，敌人固定为蝙蝠。
    /// </summary>
    [Serializable]
    public readonly struct WaveDefinition
    {
        public readonly int Index;
        public readonly float Time;
        public readonly int MinimumCount;
        public readonly float SpawnInterval;
        public readonly string EnemyId;
        public readonly string EnemyName;
        public readonly string SourceMinute;

        public WaveDefinition(
            int index,
            float time,
            int minimumCount,
            float spawnInterval,
            string enemyId,
            string enemyName,
            string sourceMinute)
        {
            Index = index;
            Time = time;
            MinimumCount = minimumCount;
            SpawnInterval = spawnInterval;
            EnemyId = enemyId;
            EnemyName = enemyName;
            SourceMinute = sourceMinute;
        }

        public override string ToString()
        {
            return $"第 {Index} 波 @ {Time:0.##}s：{EnemyName} 下限 {MinimumCount}，间隔 {SpawnInterval:0.##}s";
        }
    }
}
