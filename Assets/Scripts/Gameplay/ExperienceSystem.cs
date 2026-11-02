using System;
using SurvivorArena.Core;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 局内经验与等级（Gameplay 层）。
    /// 曲线参照《局内成长与节奏剖析》1.1：1→2 需 5 XP，2→20 每级增量 +10，
    /// 20→21 额外 +600，21 之后每级增量 +13（长局才用得到）。
    ///
    /// 只负责“等级/经验/欠了几次升级”，三选一由 UpgradeSystem 处理；
    /// XP 由 PickupSystem 通过 XpGemCollectedEvent 送进来。
    /// </summary>
    public sealed class ExperienceSystem : IDisposable
    {
        private bool _disposed;

        public ExperienceSystem()
        {
            EventBus<XpGemCollectedEvent>.Subscribe(OnXpGemCollected);
        }

        public int Level { get; private set; } = 1;

        public int Xp { get; private set; }

        public int XpToNext => GetXpToNext(Level);

        /// <summary>已升级但还没弹出三选一的次数。</summary>
        public int PendingLevelUps { get; private set; }

        public void AddXp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Xp += amount;

            while (Xp >= GetXpToNext(Level))
            {
                Xp -= GetXpToNext(Level);
                Level++;
                PendingLevelUps++;
            }

            Publish();
        }

        /// <summary>一次三选一完成/放弃后调用。</summary>
        public void ConsumeLevelUp()
        {
            if (PendingLevelUps > 0)
            {
                PendingLevelUps--;
            }

            Publish();
        }

        public void Publish()
        {
            EventBus<ExperienceChangedEvent>.Publish(
                new ExperienceChangedEvent(Level, Xp, XpToNext, PendingLevelUps));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EventBus<XpGemCollectedEvent>.Unsubscribe(OnXpGemCollected);
        }

        /// <summary>从当前等级升到下一级所需 XP。</summary>
        public static int GetXpToNext(int level)
        {
            if (level < 2)
            {
                return 5;
            }

            if (level < 20)
            {
                return 5 + (level - 1) * 10;
            }

            if (level == 20)
            {
                // 19→20 的 185 + 20 级节流阀额外 600。
                return 785;
            }

            return 785 + (level - 20) * 13;
        }

        private void OnXpGemCollected(XpGemCollectedEvent evt)
        {
            AddXp(evt.Value);
        }
    }
}
