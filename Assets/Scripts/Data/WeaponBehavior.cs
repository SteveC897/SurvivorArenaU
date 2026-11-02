namespace SurvivorArena.Data
{
    /// <summary>
    /// 武器行为类型。武器数据只描述数值，具体弹道/判定由 Gameplay.WeaponSystem 按该枚举分发。
    /// 新增武器行为时在这里加枚举，并在 WeaponSystem 加一个分支（不用为每把武器挂 MonoBehaviour）。
    /// </summary>
    public enum WeaponBehavior
    {
        MagicWand,
        Knife,
        Whip,
        Axe,
        KingBible,
        FireWand,
        Garlic,
        SantaWater,
        Runetracer,
        LightningRing
    }
}
