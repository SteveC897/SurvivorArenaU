using SurvivorArena.Core;
using SurvivorArena.Data;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 离屏回收（Gameplay 层）。
    /// 主角一直朝一个方向跑时，蝙蝠会被越甩越远；这里把超过阈值的活蝙蝠移动到
    /// 相机视野外的随机屏幕边缘，继续追击。
    ///
    /// 关键点：
    ///   - 只调用 EnemyBat.Relocate：改位置，不击杀、不回收、不重新生成；
    ///   - 通过 Core.IWorldViewport 读屏幕尺寸，不直接引用 Camera/Presentation；
    ///   - 相机不可用时退回“以主角为中心的环”。
    /// </summary>
    public sealed class OffscreenRecycler
    {
        private readonly EnemySpawner _spawner;
        private readonly Transform _player;
        private readonly BattleConfig _config;
        private readonly IWorldViewport _viewport;

        public OffscreenRecycler(
            EnemySpawner spawner,
            Transform player,
            BattleConfig config,
            IWorldViewport viewport)
        {
            _spawner = spawner;
            _player = player;
            _config = config;
            _viewport = viewport;
        }

        /// <summary>累计回收次数（只用于调试/验证，不计入击杀）。</summary>
        public int RecycledCount { get; private set; }

        /// <summary>由组合根在 LateUpdate 调用（在 KillAuraSystem 之后）。</summary>
        public void Tick()
        {
            if (_spawner == null || _player == null || _config == null)
            {
                return;
            }

            var enemies = _spawner.ActiveEnemies;
            if (enemies.Count == 0)
            {
                return;
            }

            float threshold = GetRecycleThreshold();
            float sqrThreshold = threshold * threshold;
            Vector2 playerPosition = _player.position;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyBat enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if ((enemy.Position - playerPosition).sqrMagnitude <= sqrThreshold)
                {
                    continue;
                }

                enemy.Relocate(GetRecyclePosition());
                RecycledCount++;
            }
        }

        private float GetRecycleThreshold()
        {
            // 至少要比生成半径大，避免刚生成的蝙蝠立刻被判定为“跑远了”。
            float threshold = Mathf.Max(_config.RecycleDistance, _config.EnemySpawnRadiusMax + 2f);

            if (_viewport != null &&
                _viewport.TryGetHalfExtents(out float halfWidth, out float halfHeight))
            {
                // 相机边缘到主角的最远距离约为半对角线；留 40% 余量，避免在极宽屏上
                // 把刚放到屏幕边缘的敌人又立刻判定为过远。
                float diagonal = Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
                threshold = Mathf.Max(threshold, diagonal * 1.4f);
            }

            return threshold;
        }

        private Vector2 GetRecyclePosition()
        {
            if (_viewport != null &&
                _viewport.TryGetHalfExtents(out float halfWidth, out float halfHeight))
            {
                Vector2 center = _viewport.Center;
                float margin = Mathf.Max(0.1f, _config.RecycleMargin);

                // 随机挑一条屏幕边，再在边上随机取一点，放到视野外 margin 处。
                switch (Random.Range(0, 4))
                {
                    case 0:
                        return new Vector2(
                            center.x - halfWidth - margin,
                            center.y + Random.Range(-halfHeight, halfHeight));
                    case 1:
                        return new Vector2(
                            center.x + halfWidth + margin,
                            center.y + Random.Range(-halfHeight, halfHeight));
                    case 2:
                        return new Vector2(
                            center.x + Random.Range(-halfWidth, halfWidth),
                            center.y + halfHeight + margin);
                    default:
                        return new Vector2(
                            center.x + Random.Range(-halfWidth, halfWidth),
                            center.y - halfHeight - margin);
                }
            }

            // 没有相机时退回以主角为中心的环。
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(_config.EnemySpawnRadiusMin, _config.EnemySpawnRadiusMax);
            return (Vector2)_player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }
}
