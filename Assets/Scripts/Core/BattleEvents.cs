using UnityEngine;

namespace SurvivorArena.Core
{
    /// <summary>
    /// 玩法层发给表现层的事件（Core 层）。
    /// 全部是 readonly struct，Publish 时不产生堆分配。
    /// </summary>
    public readonly struct BattleStartedEvent
    {
        public readonly int TotalWaves;
        public readonly float DurationSeconds;

        public BattleStartedEvent(int totalWaves, float durationSeconds)
        {
            TotalWaves = totalWaves;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>切到某一波时发布；HUD 只关心序号和总数。</summary>
    public readonly struct WaveStartedEvent
    {
        public readonly int WaveIndex;
        public readonly int TotalWaves;
        public readonly string EnemyName;
        public readonly int MinimumCount;
        public readonly float SpawnInterval;

        public WaveStartedEvent(
            int waveIndex,
            int totalWaves,
            string enemyName,
            int minimumCount,
            float spawnInterval)
        {
            WaveIndex = waveIndex;
            TotalWaves = totalWaves;
            EnemyName = enemyName;
            MinimumCount = minimumCount;
            SpawnInterval = spawnInterval;
        }
    }

    /// <summary>局内剩余时间；BattleSession 每帧发布，HUD 内部按整秒去重。</summary>
    public readonly struct BattleTimeChangedEvent
    {
        public readonly float RemainingSeconds;

        public BattleTimeChangedEvent(float remainingSeconds)
        {
            RemainingSeconds = remainingSeconds;
        }
    }

    /// <summary>每击杀一只敌人发布一次；TotalKills 是本局累计值。</summary>
    public readonly struct EnemyKilledEvent
    {
        public readonly int TotalKills;
        public readonly Vector2 Position;

        public EnemyKilledEvent(int totalKills, Vector2 position)
        {
            TotalKills = totalKills;
            Position = position;
        }
    }

    /// <summary>时间到、清场后发布；HUD 用这两个值填写胜利面板。</summary>
    public readonly struct BattleVictoryEvent
    {
        public readonly float SurvivedSeconds;
        public readonly int Kills;

        public BattleVictoryEvent(float survivedSeconds, int kills)
        {
            SurvivedSeconds = survivedSeconds;
            Kills = kills;
        }
    }
}
