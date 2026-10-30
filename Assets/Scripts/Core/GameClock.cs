using UnityEngine;

namespace SurvivorArena.Core
{
    /// <summary>
    /// 局内时钟（Core 层）。
    /// 只负责累计游戏时间，供 BattleSession 的波次时间轴和 HUD 倒计时使用；
    /// 不直接读 Time.time，这样以后加暂停/倍速时只需要改这一处，
    /// 不会出现“UI 停了但波次还在走”的经典问题。
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>从开局到现在的累计时间（秒）。</summary>
        public float Elapsed { get; private set; }

        /// <summary>最近一次 Tick 推进的时间（秒）；已做非负保护。</summary>
        public float DeltaTime { get; private set; }

        public void Reset()
        {
            Elapsed = 0f;
            DeltaTime = 0f;
        }

        /// <summary>由组合根每帧调用一次。</summary>
        public void Tick(float deltaTime)
        {
            DeltaTime = Mathf.Max(0f, deltaTime);
            Elapsed += DeltaTime;
        }
    }
}
