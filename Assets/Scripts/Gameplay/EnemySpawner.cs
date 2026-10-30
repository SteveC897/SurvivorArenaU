using System.Collections.Generic;
using SurvivorArena.Core;
using SurvivorArena.Data;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 按波次表维持场上蝙蝠数量下限，并按补刷间隔生成敌人（Gameplay 层）。
    /// 与 VS 的刷怪规则一致：当前波次只提供“数量下限 + 补刷间隔”，
    /// 存活数低于下限时按间隔补刷；达到下限后停止，等下一次减员再补。
    ///
    /// 依赖：
    ///   - Core.ObjectPool：蝙蝠实例池化；
    ///   - Data.BattleConfig：生成半径/速度/预热等数值；
    ///   - Core.EventBus：击杀事件通知 HUD。
    /// 不依赖 Presentation，也不直接读 Camera/Input。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("资源引用")]
        [SerializeField] private EnemyBat batPrefab;
        [SerializeField] private Sprite[] batVariants;

        private readonly List<EnemyBat> _active = new List<EnemyBat>();
        private ObjectPool<EnemyBat> _pool;
        private Transform _player;
        private BattleConfig _config;
        private Transform _poolRoot;

        private WaveDefinition _wave;
        private float _spawnInterval = 1f;
        private int _minimumCount;
        private float _spawnTimer;
        private bool _spawning;
        private int _killCount;

        public IReadOnlyList<EnemyBat> ActiveEnemies => _active;

        public int ActiveCount => _active.Count;

        public int KillCount => _killCount;

        public bool IsSpawning => _spawning;

        public WaveDefinition CurrentWave => _wave;

        /// <summary>由组合根在 Start 里注入依赖并预热对象池。</summary>
        public void Initialize(Transform player, BattleConfig config)
        {
            if (player == null || config == null)
            {
                Debug.LogError($"{nameof(EnemySpawner)} 初始化参数不完整。", this);
                enabled = false;
                return;
            }

            if (batPrefab == null)
            {
                Debug.LogError($"{nameof(EnemySpawner)} 缺少蝙蝠预制体。", this);
                enabled = false;
                return;
            }

            _player = player;
            _config = config;

            var poolRootObject = new GameObject("Pool");
            poolRootObject.transform.SetParent(transform, false);
            _poolRoot = poolRootObject.transform;

            _pool = new ObjectPool<EnemyBat>(
                CreateInstance,
                enemy => enemy.gameObject.SetActive(true),
                enemy => enemy.gameObject.SetActive(false),
                config.EnemyPrewarmCount);

            _spawning = false;
        }

        /// <summary>切换当前波次。切换后若低于下限，会立即开始补刷。</summary>
        public void SetWave(WaveDefinition wave)
        {
            _wave = wave;
            _minimumCount = Mathf.Max(1, wave.MinimumCount);
            _spawnInterval = Mathf.Max(0.01f, wave.SpawnInterval);
            _spawnTimer = _spawnInterval;
            _spawning = true;
        }

        public void StopSpawning()
        {
            _spawning = false;
        }

        /// <summary>由 BattleSession 每帧驱动，保证与 GameClock 同一节拍。</summary>
        public void Tick(float deltaTime)
        {
            if (!_spawning || _pool == null || _player == null || _config == null || deltaTime <= 0f)
            {
                return;
            }

            _spawnTimer += deltaTime;

            int spawned = 0;
            while (_spawnTimer >= _spawnInterval &&
                   _active.Count < _minimumCount &&
                   spawned < _config.EnemyMaxSpawnsPerFrame)
            {
                _spawnTimer -= _spawnInterval;
                SpawnOne();
                spawned++;
            }

            if (_active.Count >= _minimumCount)
            {
                // 已达下限时不积累计时，避免下一次减员时瞬间刷出一大批。
                _spawnTimer = Mathf.Min(_spawnTimer, _spawnInterval);
            }
        }

        /// <summary>清空场上所有蝙蝠（用于倒计时结束）。返回被清除的数量。</summary>
        public int KillAll()
        {
            if (_active.Count == 0)
            {
                return 0;
            }

            EnemyBat[] snapshot = _active.ToArray();
            int cleared = 0;
            for (int i = 0; i < snapshot.Length; i++)
            {
                EnemyBat enemy = snapshot[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                enemy.Kill();
                cleared++;
            }

            return cleared;
        }

        private void SpawnOne()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float distance = Random.Range(_config.EnemySpawnRadiusMin, _config.EnemySpawnRadiusMax);
            Vector2 position = (Vector2)_player.position + direction * distance;

            EnemyBat enemy = _pool.Get();

            Sprite sprite = null;
            if (batVariants != null && batVariants.Length > 0)
            {
                sprite = batVariants[Random.Range(0, batVariants.Length)];
            }

            enemy.Spawn(
                position,
                sprite,
                Random.Range(_config.EnemySpeedMin, _config.EnemySpeedMax),
                _player,
                _config.EnemyRadius);
            _active.Add(enemy);
        }

        private EnemyBat CreateInstance()
        {
            EnemyBat instance = Instantiate(batPrefab, _poolRoot);
            instance.name = "EnemyBat";
            instance.Killed += HandleKilled;
            instance.Released += HandleReleased;
            return instance;
        }

        private void HandleKilled(EnemyBat enemy)
        {
            if (!_active.Remove(enemy))
            {
                return;
            }

            _killCount++;
            EventBus<EnemyKilledEvent>.Publish(new EnemyKilledEvent(_killCount, enemy.Position));
        }

        private void HandleReleased(EnemyBat enemy)
        {
            if (enemy == null || _pool == null)
            {
                return;
            }

            _pool.Release(enemy);
        }
    }
}
