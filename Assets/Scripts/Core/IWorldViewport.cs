using UnityEngine;

namespace SurvivorArena.Core
{
    /// <summary>
    /// 世界可视区域抽象（Core 层）。
    /// Gameplay 的离屏回收需要知道“屏幕有多大”，但不应该直接依赖 Camera；
    /// Presentation 层用 CameraViewport 实现它，再由组合根注入。
    /// </summary>
    public interface IWorldViewport
    {
        /// <summary>视野中心（世界坐标，通常就是相机位置）。</summary>
        Vector2 Center { get; }

        /// <summary>
        /// 取正交视野的半宽/半高（世界单位）。
        /// 没有可用相机时返回 false，调用方应回退到别的落点策略。
        /// </summary>
        bool TryGetHalfExtents(out float halfWidth, out float halfHeight);
    }
}
