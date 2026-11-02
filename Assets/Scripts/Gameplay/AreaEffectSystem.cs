using System.Collections.Generic;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 地面/瞬发区域伤害（神鞭、大蒜、圣徒水、闪电戒指共用）。
    /// 表现由 CombatPresenter 读取 Active 绘制；这里只做判定。
    /// </summary>
    public sealed class AreaEffect
    {
        public Vector2 Position;
        public Vector2 Size;
        public float Radius;
        public float Angle;
        public bool IsRectangle;
        public float Damage;
        public float Duration;
        public float Age;
        public float TickInterval;
        public float NextTick;
        public bool Ticked;
        public Sprite Sprite;
        public Color Color = Color.white;
        public float VisualScale = 1f;
    }

    /// <summary>区域伤害系统：圆形 / 旋转矩形，支持单次与按间隔持续伤害。</summary>
    public sealed class AreaEffectSystem
    {
        private readonly EnemySpawner _spawner;
        private readonly List<AreaEffect> _active = new List<AreaEffect>();

        public AreaEffectSystem(EnemySpawner spawner)
        {
            _spawner = spawner;
        }

        public IReadOnlyList<AreaEffect> Active => _active;

        public AreaEffect SpawnCircle(
            Vector2 position,
            float radius,
            float damage,
            float duration,
            float tickInterval,
            Sprite sprite,
            Color? color = null,
            float visualScale = 1f)
        {
            var effect = new AreaEffect
            {
                Position = position,
                Radius = Mathf.Max(0.05f, radius),
                Size = new Vector2(radius * 2f, radius * 2f),
                Damage = damage,
                Duration = Mathf.Max(0.05f, duration),
                TickInterval = Mathf.Max(0f, tickInterval),
                NextTick = 0f,
                Sprite = sprite,
                Color = color ?? new Color(1f, 1f, 1f, 0.5f),
                VisualScale = visualScale,
                IsRectangle = false
            };

            _active.Add(effect);
            return effect;
        }

        public AreaEffect SpawnRectangle(
            Vector2 position,
            Vector2 size,
            float angle,
            float damage,
            float duration,
            Sprite sprite,
            Color? color = null)
        {
            var effect = new AreaEffect
            {
                Position = position,
                Size = size,
                Radius = Mathf.Max(size.x, size.y) * 0.5f,
                Angle = angle,
                Damage = damage,
                Duration = Mathf.Max(0.05f, duration),
                TickInterval = 0f,
                NextTick = 0f,
                Sprite = sprite,
                Color = color ?? new Color(1f, 1f, 1f, 0.5f),
                VisualScale = 1f,
                IsRectangle = true
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
                AreaEffect effect = _active[i];
                effect.Age += deltaTime;
                effect.NextTick -= deltaTime;

                if (effect.TickInterval <= 0f)
                {
                    if (!effect.Ticked)
                    {
                        ApplyDamage(effect);
                        effect.Ticked = true;
                    }
                }
                else
                {
                    while (effect.NextTick <= 0f)
                    {
                        ApplyDamage(effect);
                        effect.Ticked = true;
                        effect.NextTick += effect.TickInterval;
                    }
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

        private void ApplyDamage(AreaEffect effect)
        {
            if (_spawner == null)
            {
                return;
            }

            IReadOnlyList<EnemyBat> enemies = _spawner.ActiveEnemies;
            float cos = Mathf.Cos(-effect.Angle * Mathf.Deg2Rad);
            float sin = Mathf.Sin(-effect.Angle * Mathf.Deg2Rad);

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyBat enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                Vector2 delta = enemy.Position - effect.Position;
                bool hit;

                if (effect.IsRectangle)
                {
                    float localX = delta.x * cos - delta.y * sin;
                    float localY = delta.x * sin + delta.y * cos;
                    hit = Mathf.Abs(localX) <= effect.Size.x * 0.5f + enemy.Radius &&
                          Mathf.Abs(localY) <= effect.Size.y * 0.5f + enemy.Radius;
                }
                else
                {
                    float hitRadius = effect.Radius + enemy.Radius;
                    hit = delta.sqrMagnitude <= hitRadius * hitRadius;
                }

                if (hit)
                {
                    enemy.ApplyDamage(effect.Damage);
                }
            }
        }
    }
}
