using System;
using System.Collections.Generic;
using SurvivorArena.Core;
using SurvivorArena.Data;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>场上等待拾取的宝箱。</summary>
    public sealed class Chest
    {
        public Vector2 Position;
        public int RewardCount;
        public float Age;
        public float BobPhase;
    }

    /// <summary>
    /// 宝箱系统（Gameplay 层）：
    ///   - 按 BattleConfig.BossSpawnTimes 生成宝箱携带者（金色大蝙蝠）；
    ///   - 携带者死亡后在其位置掉宝箱（不击杀/不消失，玩家走过去开）；
    ///   - 宝箱奖励位 1/3/5 由 BossChestRewards 配置；
    ///   - 开箱直接调用 UpgradeSystem.GrantChestRewards（不弹三选一），HUD 收到 ChestOpenedEvent。
    ///
    /// 超武进化按 Docs/00-profile 第六节暂缓；宝箱当前只给武器/被动升级。
    /// </summary>
    public sealed class ChestSystem : IDisposable
    {
        private readonly EnemySpawner _spawner;
        private readonly Transform _player;
        private readonly BattleConfig _config;
        private readonly UpgradeSystem _upgrades;
        private readonly List<Chest> _chests = new List<Chest>();
        private readonly Queue<int> _pendingRewards = new Queue<int>();
        private readonly System.Random _random;
        private int _nextBossIndex;
        private float _elapsed;
        private bool _disposed;

        public ChestSystem(
            EnemySpawner spawner,
            Transform player,
            BattleConfig config,
            UpgradeSystem upgrades)
        {
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
            _random = new System.Random(config.RandomSeed + 73);

            EventBus<EnemyKilledEvent>.Subscribe(OnEnemyKilled);
        }

        public IReadOnlyList<Chest> ActiveChests => _chests;

        public int SpawnedBossCount => _nextBossIndex;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _elapsed += deltaTime;
            SpawnScheduledBosses();
            TickChests(deltaTime);
        }

        public void Clear()
        {
            _chests.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EventBus<EnemyKilledEvent>.Unsubscribe(OnEnemyKilled);
        }

        private void SpawnScheduledBosses()
        {
            float[] times = _config.BossSpawnTimes;
            if (times == null)
            {
                return;
            }

            while (_nextBossIndex < times.Length && _elapsed >= times[_nextBossIndex])
            {
                int reward = 1;
                int[] rewards = _config.BossChestRewards;
                if (rewards != null && rewards.Length > 0)
                {
                    reward = rewards[Mathf.Clamp(_nextBossIndex, 0, rewards.Length - 1)];
                }

                float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                Vector2 position = (Vector2)_player.position +
                                   new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                                   _config.BossSpawnRadius;
                float health = _config.BossHealthBase +
                               _config.BossHealthPerSpawn * _nextBossIndex;
                EnemyBat boss = _spawner.SpawnBoss(position, health, _config.BossXp);

                if (boss != null)
                {
                    _pendingRewards.Enqueue(reward);
                    EventBus<BossSpawnedEvent>.Publish(
                        new BossSpawnedEvent("宝箱蝙蝠", reward));
                    Debug.Log($"[宝箱] 宝箱蝙蝠出现 @{_elapsed:0.0}s，血量 {health:0}，奖励位 {reward}");
                }

                _nextBossIndex++;
            }
        }

        private void TickChests(float deltaTime)
        {
            if (_chests.Count == 0)
            {
                return;
            }

            float pickupRadius = Mathf.Max(0.2f, _config.ChestPickupRadius);
            Vector2 playerPosition = _player.position;

            for (int i = _chests.Count - 1; i >= 0; i--)
            {
                Chest chest = _chests[i];
                chest.Age += deltaTime;

                if ((chest.Position - playerPosition).sqrMagnitude >
                    pickupRadius * pickupRadius)
                {
                    continue;
                }

                _upgrades.GrantChestRewards(chest.RewardCount);
                _chests.RemoveAt(i);
            }
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (!evt.IsBoss)
            {
                return;
            }

            int reward = _pendingRewards.Count > 0 ? _pendingRewards.Dequeue() : 1;
            _chests.Add(new Chest
            {
                Position = evt.Position,
                RewardCount = reward,
                Age = 0f,
                BobPhase = (float)_random.NextDouble() * Mathf.PI * 2f
            });

            Debug.Log($"[宝箱] 携带者死亡，掉落宝箱 @{evt.Position}，奖励位 {reward}");
        }
    }
}
