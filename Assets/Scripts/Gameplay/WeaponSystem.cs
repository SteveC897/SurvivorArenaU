using System;
using System.Collections.Generic;
using SurvivorArena.Core.Stats;
using SurvivorArena.Data;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 集中式武器调度（Gameplay 层，M8）：
    ///   - 所有武器共用一次 Tick，不为每把武器挂 MonoBehaviour；
    ///   - 冷却 = 武器数据 CD × 玩家 Cooldown 属性；
    ///   - 伤害/范围/飞射速度/持续时间/数量都走 Core.Stats 的最终值；
    ///   - 弹道交给 ProjectileSystem、范围交给 AreaEffectSystem、环绕交给 OrbitEffectSystem。
    ///
    /// 当前 10 种行为是 docs/吸血鬼幸存者-本体武器与被动系统详解.md 第 8.3 节的简化实现；
    /// 击退/暴击/部分特殊命中间隔暂未实现，数值和目标选择按文档。
    /// </summary>
    public sealed class WeaponSystem
    {
        private readonly PlayerStats _stats;
        private readonly Loadout _loadout;
        private readonly EnemySpawner _spawner;
        private readonly Transform _player;
        private readonly PlayerMotor _motor;
        private readonly ProjectileSystem _projectiles;
        private readonly AreaEffectSystem _areas;
        private readonly OrbitEffectSystem _orbits;
        private readonly BattleConfig _config;
        private readonly System.Random _random;

        public WeaponSystem(
            PlayerStats stats,
            Loadout loadout,
            EnemySpawner spawner,
            Transform player,
            PlayerMotor motor,
            ProjectileSystem projectiles,
            AreaEffectSystem areas,
            OrbitEffectSystem orbits,
            BattleConfig config)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _motor = motor;
            _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
            _areas = areas ?? throw new ArgumentNullException(nameof(areas));
            _orbits = orbits ?? throw new ArgumentNullException(nameof(orbits));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = new System.Random(config.RandomSeed + 31);
        }

        /// <summary>由组合根每帧调用（升级暂停时不会调用）。</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            for (int i = 0; i < _loadout.Weapons.Count; i++)
            {
                WeaponRuntime weapon = _loadout.Weapons[i];
                WeaponLevelStats stats = weapon.Stats;

                if (stats.Cooldown <= 0f)
                {
                    continue;
                }

                float cooldown = Mathf.Max(0.1f, stats.Cooldown * _stats.Cooldown.Value);
                weapon.CooldownTimer += deltaTime;

                if (weapon.CooldownTimer < cooldown)
                {
                    continue;
                }

                weapon.CooldownTimer -= cooldown;
                Fire(weapon, stats, cooldown);
            }
        }

        private void Fire(WeaponRuntime weapon, WeaponLevelStats stats, float cooldown)
        {
            Vector2 playerPosition = _player.position;
            Vector2 facing = _motor != null ? _motor.Facing : Vector2.right;
            if (facing.sqrMagnitude < 0.001f)
            {
                facing = Vector2.right;
            }

            float damage = stats.Damage * _stats.Might.Value;
            float area = Mathf.Max(0.1f, stats.Area * _stats.Area.Value);
            float speed = Mathf.Max(0.1f, stats.Speed * _stats.ProjectileSpeed.Value);
            float duration = Mathf.Max(0f, stats.Duration * _stats.Duration.Value);
            int amount = Mathf.Max(1, stats.Amount + Mathf.RoundToInt(_stats.Amount.Value - 1f));
            int pierce = Mathf.Max(1, stats.Pierce);
            Sprite sprite = weapon.Definition.ProjectileSprite;
            WeaponBehavior behavior = weapon.Definition.Behavior;

            switch (behavior)
            {
                case WeaponBehavior.MagicWand:
                {
                    List<EnemyBat> targets = GetNearestEnemies(amount);
                    for (int i = 0; i < amount; i++)
                    {
                        Vector2 direction = facing;
                        if (targets.Count > 0)
                        {
                            EnemyBat target = targets[Mathf.Min(i, targets.Count - 1)];
                            direction = (target.Position - playerPosition).normalized;
                        }

                        SpawnProjectile(
                            playerPosition + direction * 0.45f,
                            direction * (_config.BaseProjectileSpeed * speed),
                            damage,
                            0.18f,
                            pierce,
                            _config.ProjectileLifetime,
                            sprite,
                            0.55f,
                            new Color(0.55f, 0.8f, 1f, 1f));
                    }

                    break;
                }

                case WeaponBehavior.Knife:
                {
                    float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
                    for (int i = 0; i < amount; i++)
                    {
                        float offset = (i - (amount - 1) * 0.5f) * 3.5f;
                        Vector2 direction = AngleToDirection(baseAngle + offset);
                        SpawnProjectile(
                            playerPosition + direction * 0.45f,
                            direction * (_config.BaseProjectileSpeed * 1.25f * speed),
                            damage,
                            0.16f,
                            pierce,
                            _config.ProjectileLifetime * 0.6f,
                            sprite,
                            0.65f,
                            Color.white);
                    }

                    break;
                }

                case WeaponBehavior.Whip:
                {
                    float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
                    SpawnWhip(playerPosition, facing, area, damage, angle);
                    if (amount >= 2)
                    {
                        SpawnWhip(playerPosition, -facing, area, damage, angle + 180f);
                    }

                    break;
                }

                case WeaponBehavior.Axe:
                {
                    for (int i = 0; i < amount; i++)
                    {
                        float offset = (i - (amount - 1) * 0.5f) * 0.35f;
                        Vector2 position = playerPosition + new Vector2(offset, 0.3f);
                        Vector2 velocity = new Vector2(facing.x * 2.2f + offset, 8.5f) * speed;
                        Projectile projectile = SpawnProjectile(
                            position,
                            velocity,
                            damage,
                            0.3f,
                            pierce,
                            duration > 0f ? duration : 1.5f,
                            sprite,
                            1f,
                            new Color(1f, 0.85f, 0.65f, 1f));
                        projectile.Gravity = -18f;
                    }

                    break;
                }

                case WeaponBehavior.KingBible:
                {
                    float orbitRadius = 1.7f * area;
                    float orbitDuration = duration > 0f ? duration : 3f;
                    _orbits.Spawn(
                        _player,
                        orbitRadius,
                        amount,
                        2.4f * speed,
                        orbitDuration,
                        damage,
                        sprite);
                    weapon.CooldownTimer = -orbitDuration;
                    break;
                }

                case WeaponBehavior.FireWand:
                {
                    for (int i = 0; i < amount; i++)
                    {
                        EnemyBat target = PickRandomEnemy();
                        Vector2 direction = target != null
                            ? (target.Position - playerPosition).normalized
                            : AngleToDirection((float)_random.NextDouble() * 360f);

                        SpawnProjectile(
                            playerPosition + direction * 0.5f,
                            direction * (_config.BaseProjectileSpeed * 0.85f * speed),
                            damage,
                            0.24f,
                            pierce,
                            _config.ProjectileLifetime,
                            sprite,
                            0.85f,
                            new Color(1f, 0.55f, 0.2f, 1f));
                    }

                    break;
                }

                case WeaponBehavior.Garlic:
                {
                    float radius = Mathf.Max(0.2f, _config.GarlicBaseRadius * area);
                    _areas.SpawnCircle(
                        playerPosition,
                        radius,
                        damage,
                        0.12f,
                        0f,
                        null,
                        new Color(0.55f, 1f, 0.55f, 0.42f));
                    break;
                }

                case WeaponBehavior.SantaWater:
                {
                    for (int i = 0; i < amount; i++)
                    {
                        float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                        float distance = 2f + (float)_random.NextDouble() * 2f;
                        Vector2 position = playerPosition +
                                           new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                        _areas.SpawnCircle(
                            position,
                            1.35f * area,
                            damage,
                            duration > 0f ? duration : 2f,
                            0.5f,
                            null,
                            new Color(0.45f, 0.75f, 1f, 0.38f));
                    }

                    break;
                }

                case WeaponBehavior.Runetracer:
                {
                    for (int i = 0; i < amount; i++)
                    {
                        float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                        Vector2 direction = AngleToDirection(angle);
                        Projectile projectile = SpawnProjectile(
                            playerPosition + direction * 0.5f,
                            direction * (_config.BaseProjectileSpeed * 1.1f * speed),
                            damage,
                            0.28f,
                            Mathf.Max(99, pierce),
                            duration > 0f ? duration : 2.25f,
                            sprite,
                            0.9f,
                            new Color(0.8f, 0.6f, 1f, 1f));
                        projectile.Bounce = true;
                    }

                    break;
                }

                case WeaponBehavior.LightningRing:
                {
                    for (int i = 0; i < amount; i++)
                    {
                        EnemyBat target = PickRandomEnemy();
                        if (target == null)
                        {
                            continue;
                        }

                        _areas.SpawnCircle(
                            target.Position,
                            1.1f * area,
                            damage,
                            0.14f,
                            0f,
                            null,
                            new Color(1f, 1f, 0.45f, 0.7f));
                    }

                    break;
                }
            }
        }

        private void SpawnWhip(Vector2 playerPosition, Vector2 direction, float area, float damage, float angle)
        {
            Vector2 center = playerPosition + direction * (1.55f * area);
            _areas.SpawnRectangle(
                center,
                new Vector2(3.2f * area, 1.5f * area),
                angle,
                damage,
                0.12f,
                null,
                new Color(1f, 0.9f, 0.45f, 0.35f));
        }

        private Projectile SpawnProjectile(
            Vector2 position,
            Vector2 velocity,
            float damage,
            float radius,
            int pierce,
            float lifetime,
            Sprite sprite,
            float scale,
            Color color)
        {
            return _projectiles.Spawn(
                position,
                velocity,
                damage,
                radius,
                pierce,
                lifetime,
                sprite,
                scale,
                false,
                0f,
                color);
        }

        private List<EnemyBat> GetNearestEnemies(int count)
        {
            var result = new List<EnemyBat>(count);
            IReadOnlyList<EnemyBat> enemies = _spawner.ActiveEnemies;
            Vector2 center = _player.position;

            for (int n = 0; n < count; n++)
            {
                EnemyBat nearest = null;
                float nearestSqr = float.MaxValue;

                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyBat enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive || result.Contains(enemy))
                    {
                        continue;
                    }

                    float sqr = (enemy.Position - center).sqrMagnitude;
                    if (sqr < nearestSqr)
                    {
                        nearestSqr = sqr;
                        nearest = enemy;
                    }
                }

                if (nearest == null)
                {
                    break;
                }

                result.Add(nearest);
            }

            return result;
        }

        private EnemyBat PickRandomEnemy()
        {
            IReadOnlyList<EnemyBat> enemies = _spawner.ActiveEnemies;
            int alive = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive)
                {
                    alive++;
                }
            }

            if (alive == 0)
            {
                return null;
            }

            int pick = _random.Next(0, alive);
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyBat enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (pick == 0)
                {
                    return enemy;
                }

                pick--;
            }

            return null;
        }

        private static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
