using System;
using System.Collections.Generic;

namespace SurvivorArena.Core
{
    /// <summary>
    /// 通用对象池（Core 层）。
    /// 只负责“借出/归还/预热/计数”，不关心 T 是敌人、投射物还是 UI 格子：
    ///   - create：池空时如何创建；
    ///   - onGet：借出前（例如 SetActive(true)、重置状态）；
    ///   - onRelease：归还前（例如 SetActive(false)、清理事件）。
    ///
    /// 使用方：Gameplay.EnemySpawner 用它池化 EnemyBat，避免 0.1s/只的生成频率造成
    /// Instantiate/Destroy 抖动和 GC。
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _available = new Stack<T>();

        private int _activeCount;
        private int _createdCount;

        public ObjectPool(
            Func<T> create,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            int prewarmCount = 0)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet;
            _onRelease = onRelease;

            int count = Math.Max(0, prewarmCount);
            for (int i = 0; i < count; i++)
            {
                T item = CreateItem();
                _onRelease?.Invoke(item);
                _available.Push(item);
            }
        }

        /// <summary>当前借出、尚未归还的对象数量。</summary>
        public int ActiveCount => _activeCount;

        /// <summary>池里空闲对象数量。</summary>
        public int AvailableCount => _available.Count;

        /// <summary>累计创建过的对象数量（预热 + 不够用时扩容）。</summary>
        public int CreatedCount => _createdCount;

        public T Get()
        {
            T item = _available.Count > 0 ? _available.Pop() : CreateItem();
            _onGet?.Invoke(item);
            _activeCount++;
            return item;
        }

        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }

            _onRelease?.Invoke(item);
            _available.Push(item);

            if (_activeCount > 0)
            {
                _activeCount--;
            }
        }

        private T CreateItem()
        {
            T item = _create();
            if (item == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ObjectPool<T>)} 的 create 回调返回了 null（类型 {typeof(T).Name}）。");
            }

            _createdCount++;
            return item;
        }
    }
}
