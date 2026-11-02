using SurvivorArena.Data;

namespace SurvivorArena.Gameplay
{
    /// <summary>一把已持有武器的运行时状态（等级、冷却计时）。</summary>
    public sealed class WeaponRuntime
    {
        public WeaponRuntime(WeaponDefinition definition)
        {
            Definition = definition;
            Level = 1;
            CooldownTimer = 0.2f;
        }

        public WeaponDefinition Definition { get; }

        public int Level { get; private set; }

        public float CooldownTimer { get; set; }

        public bool IsMaxLevel => Definition.IsMaxLevel(Level);

        public WeaponLevelStats Stats => Definition.GetLevelStats(Level);

        public bool TryUpgrade()
        {
            if (IsMaxLevel)
            {
                return false;
            }

            Level++;
            return true;
        }
    }
}
