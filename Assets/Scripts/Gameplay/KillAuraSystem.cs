using System;
using SurvivorArena.Core.Stats;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 测试用攻击方式（Gameplay 层）：以主角为圆心、半径约 2 个身位的圆形判定区。
    /// 敌人身体碰到圆环即死亡（中心距离 &lt;= 半径 + 敌人半径）。
    /// 这里只有判定，没有贴图/脉冲等表现——那些在 Presentation.AuraView 里。
    ///
    /// 半径来自 Core.Stats.Stat（AuraRadius）；加成通过 AddModifier 注册，
    /// 本系统只读 Value，不缓存倍率，也不直接改半径。
    /// </summary>
    public sealed class KillAuraSystem
    {
        private readonly EnemySpawner _spawner;
        private readonly Transform _center;
        private readonly Stat _radius;

        public KillAuraSystem(EnemySpawner spawner, Transform center, Stat radius)
        {
            _spawner = spawner;
            _center = center;
            _radius = radius ?? throw new ArgumentNullException(nameof(radius));
        }

        /// <summary>可临时关闭（调试/测试用）。</summary>
        public bool Enabled { get; set; } = true;

        public float Radius => _radius.Value;

        /// <summary>由组合根在 LateUpdate 调用：此时主角与蝙蝠本帧的移动都已结束。</summary>
        public void Tick()
        {
            if (!Enabled || _spawner == null || _center == null)
            {
                return;
            }

            var enemies = _spawner.ActiveEnemies;
            if (enemies.Count == 0)
            {
                return;
            }

            float radius = _radius.Value;
            Vector2 center = _center.position;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyBat enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                float hitRadius = radius + enemy.Radius;
                if ((enemy.Position - center).sqrMagnitude <= hitRadius * hitRadius)
                {
                    enemy.Kill();
                }
            }
        }
    }
}
