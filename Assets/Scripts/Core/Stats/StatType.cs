namespace SurvivorArena.Core.Stats
{
    /// <summary>
    /// 角色属性类型（Core.Stats）。
    /// 前 12 项对齐《技术实现清单》M2 的核心属性，后面是成长/贪欲/诅咒/复活。
    ///
    /// 单位约定：
    ///   - Might / Cooldown / Area / ProjectileSpeed / Duration / Magnet / Luck /
    ///     Growth / Greed / Curse：倍率，1 = 100%；
    ///   - MoveSpeed：世界单位（绝对速度）；
    ///   - MaxHealth / Recovery / Armor / Revival：数值；
    ///   - Amount：发射数量，最终使用时四舍五入为整数。
    /// </summary>
    public enum StatType
    {
        Might,
        Armor,
        MaxHealth,
        Recovery,
        Cooldown,
        Area,
        ProjectileSpeed,
        Duration,
        Amount,
        MoveSpeed,
        Magnet,
        Luck,
        Growth,
        Greed,
        Curse,
        Revival
    }
}
