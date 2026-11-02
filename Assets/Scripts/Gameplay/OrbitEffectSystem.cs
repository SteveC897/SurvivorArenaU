using System.Collections.Generic;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>国王圣经的环绕伤害（只保留一条环绕效果，避免重复开火叠加）。</summary>
    public sealed class OrbitEffect
    {
        public Transform Center;
        public float Radius;
        public int Count;
        public float AngularSpeed;
        public float Duration;
        public float Age;
        public float Damage;
        public float TickInterval;
        public float NextTick;
        public float Angle;
        public Sprite Sprite;
        public float VisualScale = 0.7f;
    }

    public sealed class OrbitEffectSystem
    {
        private readonly EnemySpawner _spawner;
        private readonly List<OrbitEffect> _active = new List<OrbitEffect>();

        public OrbitEffectSystem(EnemySpawner spawner)
        {
            _spawner = spawner;
        }

        public IReadOnlyList<OrbitEffect> Active => _active;

        public OrbitEffect Spawn(
            Transform center,
            float radius,
            int count,
            float angularSpeed,
            float duration,
            float damage,
            Sprite sprite)
        {
            var effect = new OrbitEffect
            {
                Center = center,
                Radius = Mathf.Max(0.2f, radius),
                Count = Mathf.Max(1, count),
                AngularSpeed = angularSpeed,
                Duration = Mathf.Max(0.1f, duration),
                Damage = damage,
                TickInterval = 1.7f,
                NextTick = 0f,
                Sprite = sprite
            };

            _active.Add(effect);
            return effect;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _active.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _active.Count;)
            {
                OrbitEffect effect = _active[i];
                effect.Age += deltaTime;
                effect.Angle += effect.AngularSpeed * deltaTime;
                effect.NextTick -= deltaTime;

                if (effect.NextTick <= 0f)
                {
                    effect.NextTick += effect.TickInterval;
                    ApplyDamage(effect);
                }

                if (effect.Age >= effect.Duration)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                i++;
            }
        }

        public void Clear()
        {
            _active.Clear();
        }

        private void ApplyDamage(OrbitEffect effect)
        {
            if (_spawner == null || effect.Center == null)
            {
                return;
            }

            IReadOnlyList<EnemyBat> enemies = _spawner.ActiveEnemies;
            Vector2 center = effect.Center.position;

            for (int e = enemies.Count - 1; e >= 0; e--)
            {
                EnemyBat enemy = enemies[e];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                // 只要敌人落在环绕圆环附近就受击；书本身的数量只影响表现。
                float distance = Vector2.Distance(enemy.Position, center);
                float ring = Mathf.Abs(distance - effect.Radius);
                if (ring <= 0.8f + enemy.Radius)
                {
                    enemy.ApplyDamage(effect.Damage);
                }
            }
        }
    }
}
