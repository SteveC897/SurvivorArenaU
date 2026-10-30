using System;

namespace SurvivorArena.Core
{
    /// <summary>
    /// 强类型事件总线（Core 层）。
    /// 每种事件 T 拥有独立的静态订阅者列表：
    ///   EventBus&lt;EnemyKilledEvent&gt;.Publish(...)
    /// 玩法层只负责 Publish，表现层/HUD 只负责 Subscribe，双方不需要互相引用。
    ///
    /// 约定：
    ///   - 事件用 readonly struct，避免每次触发都分配堆内存；
    ///   - 订阅方必须在 OnDisable/OnDestroy 里 Unsubscribe，避免场景切换后残留；
    ///   - 事件里不要做重活（例如每帧查询全场景）。
    /// </summary>
    public static class EventBus<T>
    {
        private static Action<T> _handlers;

        public static void Subscribe(Action<T> handler)
        {
            if (handler != null)
            {
                _handlers += handler;
            }
        }

        public static void Unsubscribe(Action<T> handler)
        {
            if (handler != null)
            {
                _handlers -= handler;
            }
        }

        public static void Publish(T evt)
        {
            _handlers?.Invoke(evt);
        }

        /// <summary>仅用于测试/场景重置；正常流程靠订阅方自己退订。</summary>
        public static void Clear()
        {
            _handlers = null;
        }
    }
}
