using System.Collections.Generic;
using SurvivorArena.Core;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>一枚飞行中的投射物（纯数据 + 一份表现快照）。</summary>
    public sealed class Projectile
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Damage;
        public float Radius;
        public float Scale;
        public float Lifetime;
        public float Age;
        public float Gravity;
        public float Rotation;
        public int Pierce;
        public bool Bounce;
        public Sprite Sprite;
        public Color Color = Color.white;
        public readonly List<EnemyBat> HitEnemies = new List<EnemyBat>();
    }

    /// <summary>
    /// 投射物集中管理（Gameplay 层）：
    ///   - 所有武器共用一份数组，不在每把武器/每枚弹体上挂 MonoBehaviour；
    ///   - 统一处理寿命、重力、反弹、命中、穿透；
    ///   - Presentation.CombatPresenter 读取 Active 画 Sprite。
    /// </summary>
    public sealed class ProjectileSystem
    {
        private readonly EnemySpawner _spawner;
        private readonly IWorldViewport _viewport;
        private readonly List<Projectile> _active = new List<Projectile>();

        public ProjectileSystem(EnemySpawner spawner, IWorldViewport viewport)
        {
            _spawner = spawner;
            _viewport = viewport;
        }

        public IReadOnlyList<Projectile> Active => _active;

        public Projectile Spawn(
            Vector2 position,
            Vector2 velocity,
            float damage,
            float radius,
            int pierce,
            float lifetime,
            Sprite sprite,
            float scale,
            bool bounce = false,
            float gravity = 0f,
            Color? color = null)
        {
            var projectile = new Projectile
            {
                Position = position,
                Velocity = velocity,
                Damage = damage,
                Radius = radius,
                Scale = scale,
                Lifetime = Mathf.Max(0.05f, lifetime),
                Gravity = gravity,
                Pierce = Mathf.Max(1, pierce),
                Bounce = bounce,
                Sprite = sprite,
                Color = color ?? Color.white,
                Rotation = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg
            };

            _active.Add(projectile);
            return projectile;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _active.Count == 0)
            {
                return;
            }

            IReadOnlyList<EnemyBat> enemies = _spawner != null ? _spawner.ActiveEnemies : null;

            for (int i = 0; i < _active.Count;)
            {
                Projectile projectile = _active[i];
                projectile.Age += deltaTime;

                if (projectile.Age >= projectile.Lifetime)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                projectile.Velocity.y += projectile.Gravity * deltaTime;
                projectile.Position += projectile.Velocity * deltaTime;

                if (projectile.Velocity.sqrMagnitude > 0.0001f)
                {
                    projectile.Rotation =
                        Mathf.Atan2(projectile.Velocity.y, projectile.Velocity.x) * Mathf.Rad2Deg;
                }

                if (projectile.Bounce)
                {
                    BounceInViewport(projectile);
                }

                bool removed = false;
                if (enemies != null)
                {
                    for (int e = enemies.Count - 1; e >= 0; e--)
                    {
                        EnemyBat enemy = enemies[e];
                        if (enemy == null || !enemy.IsAlive || projectile.HitEnemies.Contains(enemy))
                        {
                            continue;
                        }

                        float hitRadius = projectile.Radius + enemy.Radius;
                        if ((enemy.Position - projectile.Position).sqrMagnitude > hitRadius * hitRadius)
                        {
                            continue;
                        }

                        enemy.ApplyDamage(projectile.Damage);
                        projectile.HitEnemies.Add(enemy);
                        projectile.Pierce--;

                        if (projectile.Pierce <= 0)
                        {
                            _active.RemoveAt(i);
                            removed = true;
                            break;
                        }
                    }
                }

                if (!removed)
                {
                    i++;
                }
            }
        }

        public void Clear()
        {
            _active.Clear();
        }

        private void BounceInViewport(Projectile projectile)
        {
            if (_viewport == null ||
                !_viewport.TryGetHalfExtents(out float halfWidth, out float halfHeight))
            {
                return;
            }

            Vector2 center = _viewport.Center;
            float minX = center.x - halfWidth;
            float maxX = center.x + halfWidth;
            float minY = center.y - halfHeight;
            float maxY = center.y + halfHeight;
            bool bounced = false;

            if (projectile.Position.x < minX || projectile.Position.x > maxX)
            {
                projectile.Velocity.x = -projectile.Velocity.x;
                projectile.Position.x = Mathf.Clamp(projectile.Position.x, minX, maxX);
                bounced = true;
            }

            if (projectile.Position.y < minY || projectile.Position.y > maxY)
            {
                projectile.Velocity.y = -projectile.Velocity.y;
                projectile.Position.y = Mathf.Clamp(projectile.Position.y, minY, maxY);
                bounced = true;
            }

            if (bounced)
            {
                // 反弹后允许再次命中同一敌人，符合痕印石绕场多次伤害的直觉。
                projectile.HitEnemies.Clear();
            }
        }
    }
}
