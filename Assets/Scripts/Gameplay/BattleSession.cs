using SurvivorArena.Core;
using SurvivorArena.Data;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 单局战斗流程（Gameplay 层）：
    /// 维护局内时间 → 让 WaveScheduler 切波 → 驱动 EnemySpawner → 发布 HUD 事件 → 到点胜利。
    /// 不直接引用 HUD/Presentation，全部通过 Core.EventBus 通知。
    /// </summary>
    public sealed class BattleSession
    {
        private readonly GameClock _clock;
        private readonly WaveScheduler _scheduler;
        private readonly EnemySpawner _spawner;
        private readonly PlayerMotor _player;
        private readonly BattleConfig _config;
        private readonly bool _logWaveStarts;

        private bool _victory;

        public BattleSession(
            GameClock clock,
            WaveScheduler scheduler,
            EnemySpawner spawner,
            PlayerMotor player,
            BattleConfig config,
            bool logWaveStarts)
        {
            _clock = clock;
            _scheduler = scheduler;
            _spawner = spawner;
            _player = player;
            _config = config;
            _logWaveStarts = logWaveStarts;

            _scheduler.WaveTriggered += HandleWaveTriggered;
        }

        public bool IsVictory => _victory;

        public float Elapsed => Mathf.Min(_clock.Elapsed, _config.RunDurationSeconds);

        public int Kills => _spawner != null ? _spawner.KillCount : 0;

        /// <summary>由组合根在 Start 调用一次。</summary>
        public void Start()
        {
            _clock.Reset();
            _victory = false;
            _scheduler.Reset();

            EventBus<BattleStartedEvent>.Publish(
                new BattleStartedEvent(_scheduler.TotalWaves, _config.RunDurationSeconds));
            EventBus<BattleTimeChangedEvent>.Publish(
                new BattleTimeChangedEvent(_config.RunDurationSeconds));

            _scheduler.Advance(0f);

            Debug.Log($"[波次表] 已加载 {_scheduler.Table.Summary}；单局 {_config.RunDurationSeconds:0}s。");
        }

        /// <summary>由组合根每帧调用；GameClock 已由组合根 Tick 过。</summary>
        public void Tick(float deltaTime)
        {
            if (_victory)
            {
                return;
            }

            float elapsed = Elapsed;
            _scheduler.Advance(elapsed);
            _spawner.Tick(deltaTime);

            EventBus<BattleTimeChangedEvent>.Publish(
                new BattleTimeChangedEvent(_config.RunDurationSeconds - elapsed));

            if (_clock.Elapsed >= _config.RunDurationSeconds)
            {
                Win();
            }
        }

        private void HandleWaveTriggered(WaveDefinition wave)
        {
            _spawner.SetWave(wave);

            EventBus<WaveStartedEvent>.Publish(new WaveStartedEvent(
                wave.Index,
                _scheduler.TotalWaves,
                wave.EnemyName,
                wave.MinimumCount,
                wave.SpawnInterval));

            if (_logWaveStarts)
            {
                Debug.Log(
                    $"[波次] {wave.Index}/{_scheduler.TotalWaves} @{wave.Time:0.#}s：" +
                    $"{wave.EnemyName} 下限 {wave.MinimumCount}，间隔 {wave.SpawnInterval:0.##}s" +
                    $"（源表 {wave.SourceMinute}）");
            }
        }

        private void Win()
        {
            _victory = true;
            _spawner.StopSpawning();

            int cleared = _spawner.KillAll();
            _player.SetControlEnabled(false);

            EventBus<BattleTimeChangedEvent>.Publish(new BattleTimeChangedEvent(0f));
            EventBus<BattleVictoryEvent>.Publish(
                new BattleVictoryEvent(_config.RunDurationSeconds, _spawner.KillCount));

            Debug.Log(
                $"[战斗结束] 胜利。存活 {_config.RunDurationSeconds:0.0}s，" +
                $"清场 {cleared}，总击杀 {_spawner.KillCount}。");
        }
    }
}
