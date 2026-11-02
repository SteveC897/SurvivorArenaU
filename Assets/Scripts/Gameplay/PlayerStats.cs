using SurvivorArena.Core.Stats;
using SurvivorArena.Data;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 玩家属性表（Gameplay 层）。
    /// 负责把 Data.BattleConfig 里的基础值 + 本测试场景的默认值装进一个 StatBlock；
    /// 武器/被动/强化只拿到这里的 Stat 引用去 AddModifier，不允许直接改 Stat.Value。
    ///
    /// 当前真正读取的属性：
    ///   - MoveSpeed：PlayerMotor；
    ///   - Might / Cooldown / Area / ProjectileSpeed / Duration / Amount：WeaponSystem；
    ///   - Magnet：PickupSystem；
    ///   - Armor / MaxHealth / Recovery：防御被动保留，等玩家受伤流程接入。
    /// </summary>
    public sealed class PlayerStats
    {
        private readonly StatBlock _block;

        public PlayerStats(BattleConfig config)
        {
            if (config == null)
            {
                throw new System.ArgumentNullException(nameof(config));
            }

            _block = new StatBlock(new[]
            {
                // 倍率类：1 = 100%
                new Stat(StatType.Might, 1f, 0f),
                new Stat(StatType.Cooldown, 1f, 0.1f),
                new Stat(StatType.Area, 1f, 0.1f),
                new Stat(StatType.ProjectileSpeed, 1f, 0.1f),
                new Stat(StatType.Duration, 1f, 0.1f),
                new Stat(StatType.Magnet, 1f, 0f),
                new Stat(StatType.Luck, 1f, 0f),
                new Stat(StatType.Growth, 1f, 0f),
                new Stat(StatType.Greed, 1f, 0f),
                new Stat(StatType.Curse, 1f, 0f),

                // 数值类
                new Stat(StatType.Armor, 0f, 0f),
                new Stat(StatType.MaxHealth, 100f, 1f),
                new Stat(StatType.Recovery, 0f, 0f),
                new Stat(StatType.Amount, 1f, 1f),
                new Stat(StatType.Revival, 0f, 0f),

                // 绝对值类：来自 BattleConfig
                new Stat(StatType.MoveSpeed, config.PlayerMoveSpeed, 0f)
            });
        }

        public StatBlock Block => _block;

        public Stat Get(StatType type)
        {
            return _block.Get(type);
        }

        public Stat MoveSpeed => Get(StatType.MoveSpeed);

        public Stat Might => Get(StatType.Might);

        public Stat Armor => Get(StatType.Armor);

        public Stat MaxHealth => Get(StatType.MaxHealth);

        public Stat Recovery => Get(StatType.Recovery);

        public Stat Cooldown => Get(StatType.Cooldown);

        public Stat Area => Get(StatType.Area);

        public Stat ProjectileSpeed => Get(StatType.ProjectileSpeed);

        public Stat Duration => Get(StatType.Duration);

        public Stat Amount => Get(StatType.Amount);

        public Stat Magnet => Get(StatType.Magnet);

        public Stat Luck => Get(StatType.Luck);

        public Stat Growth => Get(StatType.Growth);

        public Stat Greed => Get(StatType.Greed);

        public Stat Curse => Get(StatType.Curse);

        public Stat Revival => Get(StatType.Revival);
    }
}
