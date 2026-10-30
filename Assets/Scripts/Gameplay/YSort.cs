using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 2D 俯视角的 Y 轴排序（Gameplay 共享工具）。
    /// y 越小（越靠近屏幕下方）order 越大，从而压在别的单位上面。
    /// 环境元素使用固定的极低 order，保证始终位于所有单位之下。
    /// </summary>
    public static class YSort
    {
        public const int GroundOrder = -10000;
        public const int AuraOrder = -5000;

        public static int OrderForY(float y)
        {
            return Mathf.Clamp(Mathf.RoundToInt(-y * 10f), -4000, 4000);
        }
    }
}
