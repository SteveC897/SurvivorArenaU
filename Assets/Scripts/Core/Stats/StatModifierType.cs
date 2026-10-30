namespace SurvivorArena.Core.Stats
{
    /// <summary>
    /// 修改器类型。计算顺序固定为：
    ///   1. FlatAdd      —— 直接加到 base 上；
    ///   2. PercentAdd   —— 先各自求和，再乘 (1 + 总和)；
    ///   3. PercentMul   —— 每个都乘 (1 + Value)，多个连乘。
    ///
    /// 例：base 100，Flat +20，PercentAdd +50%，PercentMul +50%
    ///     = (100 + 20) * (1 + 0.5) * (1 + 0.5) = 270
    /// </summary>
    public enum StatModifierType
    {
        FlatAdd,
        PercentAdd,
        PercentMul
    }
}
