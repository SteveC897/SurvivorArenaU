using SurvivorArena.Data;

namespace SurvivorArena.Gameplay
{
    /// <summary>一件已持有被动的运行时状态。</summary>
    public sealed class PassiveRuntime
    {
        public PassiveRuntime(PassiveDefinition definition)
        {
            Definition = definition;
            Level = 1;
        }

        public PassiveDefinition Definition { get; }

        public int Level { get; private set; }

        public bool IsMaxLevel => Definition.IsMaxLevel(Level);

        public bool Applied { get; internal set; }

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
