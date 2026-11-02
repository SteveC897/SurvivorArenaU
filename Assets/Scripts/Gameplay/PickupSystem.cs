using System;
using System.Collections.Generic;
using SurvivorArena.Core;
using SurvivorArena.Data;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>场上的一颗经验宝石（纯数据，表现由 CombatPresenter 画）。</summary>
    public sealed class XpGem
    {
        public Vector2 Position;
        public int Value;
        public float Age;
        public float MagnetAge;
        public float BobPhase;
        public bool Magnetized;
    }

    /// <summary>
    /// 经验宝石拾取（Gameplay 层，M7/M12 的最小实现）：
    ///   - 监听 EnemyKilledEvent，按敌人 Xp 掉落；
    ///   - 宝石数量上限 256，超过时淘汰最早生成的一颗；
    ///   - 玩家 Magnet 属性决定吸附半径；
    ///   - 先慢后快地向玩家加速，进入拾取半径后发布 XpGemCollectedEvent；
    ///   - 拾取结算不在表现层，CombatPresenter 只读 ActiveGems 画宝石。
    /// </summary>
    public sealed class PickupSystem : IDisposable
    {
        private readonly Transform _player;
        private readonly BattleConfig _config;
        private readonly PlayerStats _stats;
        private readonly List<XpGem> _gems = new List<XpGem>();
        private readonly System.Random _random;
        private bool _disposed;

        public PickupSystem(Transform player, BattleConfig config, PlayerStats stats)
        {
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _stats = stats != null ? stats : throw new ArgumentNullException(nameof(stats));
            _random = new System.Random(config.RandomSeed + 17);

            EventBus<EnemyKilledEvent>.Subscribe(OnEnemyKilled);
        }

        public IReadOnlyList<XpGem> ActiveGems => _gems;

        public int Count => _gems.Count;

        public void Tick(float deltaTime)
        {
            if (_player == null || deltaTime <= 0f || _gems.Count == 0)
            {
                return;
            }

            float magnetRadius = Mathf.Max(0.1f, _config.XpGemMagnetRadius * _stats.Magnet.Value);
            float pickupRadius = Mathf.Max(0.05f, _config.XpGemPickupRadius);
            float maxSpeed = Mathf.Max(1f, _config.XpGemMagnetSpeed);
            Vector2 playerPosition = _player.position;

            for (int i = _gems.Count - 1; i >= 0; i--)
            {
                XpGem gem = _gems[i];
                gem.Age += deltaTime;

                Vector2 toPlayer = playerPosition - gem.Position;
                float distance = toPlayer.magnitude;

                if (distance <= magnetRadius)
                {
                    if (!gem.Magnetized)
                    {
                        gem.Magnetized = true;
                        gem.MagnetAge = 0f;
                    }

                    gem.MagnetAge += deltaTime;
                    float t = Mathf.Clamp01(gem.MagnetAge / 0.6f);
                    float speed = Mathf.Lerp(2f, maxSpeed, t * t);
                    gem.Position = Vector2.MoveTowards(gem.Position, playerPosition, speed * deltaTime);

                    if (distance <= pickupRadius)
                    {
                        EventBus<XpGemCollectedEvent>.Publish(
                            new XpGemCollectedEvent(gem.Value, gem.Position));
                        _gems.RemoveAt(i);
                        continue;
                    }
                }

                if (gem.Position.sqrMagnitude > 400f * 400f)
                {
                    _gems.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            _gems.Clear();
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

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (evt.Xp <= 0)
            {
                return;
            }

            Spawn(evt.Position, evt.Xp);
        }

        private void Spawn(Vector2 position, int value)
        {
            if (_gems.Count >= Mathf.Max(1, _config.XpGemMaxCount))
            {
                _gems.RemoveAt(0);
            }

            var gem = new XpGem
            {
                Position = position + new Vector2(
                    RandomRange(-0.18f, 0.18f),
                    RandomRange(-0.18f, 0.18f)),
                Value = Mathf.Max(1, value),
                Age = 0f,
                MagnetAge = 0f,
                BobPhase = RandomRange(0f, Mathf.PI * 2f)
            };

            _gems.Add(gem);
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }
    }
}
