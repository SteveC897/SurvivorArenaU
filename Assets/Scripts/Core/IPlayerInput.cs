using UnityEngine;

namespace SurvivorArena.Core
{
    /// <summary>
    /// 输入抽象（Core 层）。Gameplay 只依赖这个接口，不知道底层是旧 Input Manager
    /// 还是新 Input System；Platform 层负责具体实现。
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>当前移动输入，已归一化到长度 ≤ 1。</summary>
        Vector2 Move { get; }

        /// <summary>本帧是否按下重开键（只在胜利后使用）。</summary>
        bool RestartPressed { get; }

        /// <summary>每帧由组合根调用一次，刷新上面的属性。</summary>
        void Poll();
    }
}
