namespace SurvivorArena.Data
{
    /// <summary>
    /// 被动道具作用到哪条属性。Gameplay.PassiveRuntime 把它映射到 Core.Stats.StatType，
    /// Data 层不直接依赖 Core。
    /// </summary>
    public enum PassiveEffect
    {
        Might,
        Cooldown,
        Area,
        ProjectileSpeed,
        Duration,
        Amount,
        Magnet,
        Armor,
        MaxHealth,
        Recovery
    }

    /// <summary>被动修改器的叠加层；和 Core.Stats.StatModifierType 一一对应。</summary>
    public enum PassiveModifierKind
    {
        FlatAdd,
        PercentAdd,
        PercentMul
    }
}
