using System;
using System.Collections.Generic;
using SurvivorArena.Core;
using SurvivorArena.Data;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 局内升级三选一（Gameplay 层）。
    ///
    /// 候选池：
    ///   - 已持有且未满级的武器升级；
    ///   - 未持有且还有武器槽的新武器；
    ///   - 已持有且未满级的被动升级；
    ///   - 未持有、还有被动槽、且 Definition.Offered 的新被动。
    /// 每项的权重来自内容目录；已持有项权重 ×3，鼓励养成一条线；同一次三选一不重复。
    /// 抽卡用整数权重，避免浮点边界偏差；随机数固定 seed，便于复现。
    ///
    /// 升级时游戏暂停由 BattleBootstrap 读 IsChoosing 实现，不使用 Time.timeScale。
    /// UI 通过 EventBus 拿 LevelUpOfferedEvent，点击后回传 LevelUpChoiceEvent。
    /// </summary>
    public sealed class UpgradeSystem : IDisposable
    {
        private enum UpgradeKind
        {
            NewWeapon,
            UpgradeWeapon,
            NewPassive,
            UpgradePassive
        }

        private readonly struct Candidate
        {
            public readonly UpgradeKind Kind;
            public readonly WeaponDefinition Weapon;
            public readonly PassiveDefinition Passive;
            public readonly WeaponRuntime WeaponRuntime;
            public readonly PassiveRuntime PassiveRuntime;
            public readonly int Weight;
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly int TargetLevel;
            public readonly int MaxLevel;
            public readonly UnityEngine.Sprite Icon;
            public readonly bool IsWeapon;

            public Candidate(
                UpgradeKind kind,
                WeaponDefinition weapon,
                PassiveDefinition passive,
                WeaponRuntime weaponRuntime,
                PassiveRuntime passiveRuntime,
                int weight,
                string id,
                string displayName,
                string description,
                int targetLevel,
                int maxLevel,
                UnityEngine.Sprite icon,
                bool isWeapon)
            {
                Kind = kind;
                Weapon = weapon;
                Passive = passive;
                WeaponRuntime = weaponRuntime;
                PassiveRuntime = passiveRuntime;
                Weight = weight;
                Id = id;
                DisplayName = displayName;
                Description = description;
                TargetLevel = targetLevel;
                MaxLevel = maxLevel;
                Icon = icon;
                IsWeapon = isWeapon;
            }
        }

        private readonly Loadout _loadout;
        private readonly ExperienceSystem _experience;
        private readonly ContentCatalog _catalog;
        private readonly Random _random;
        private readonly List<Candidate> _current = new List<Candidate>();
        private bool _disposed;

        public UpgradeSystem(
            PlayerStats stats,
            Loadout loadout,
            ExperienceSystem experience,
            ContentCatalog catalog,
            int seed)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _experience = experience ?? throw new ArgumentNullException(nameof(experience));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _random = new Random(seed);

            EventBus<LevelUpChoiceEvent>.Subscribe(OnChoice);
        }

        public bool IsChoosing { get; private set; }

        /// <summary>由组合根每帧调用；有待消费的升级时打开三选一。</summary>
        public void Tick()
        {
            if (IsChoosing || _experience.PendingLevelUps <= 0)
            {
                return;
            }

            OpenOffer();
        }

        /// <summary>宝箱奖励：不弹三选一，直接按奖励位随机升级；用于宝箱系统。</summary>
        public void GrantChestRewards(int count)
        {
            count = Math.Max(1, count);
            var names = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                List<Candidate> pool = BuildCandidates();
                if (pool.Count == 0)
                {
                    break;
                }

                Candidate candidate = PickWeighted(pool);
                Apply(candidate);
                names.Add(candidate.DisplayName);
            }

            if (names.Count == 0)
            {
                // 所有内容满级/槽满：按文档的保底思路给经验（当前没有金币/烤鸡系统）。
                _experience.AddXp(25);
                names.Add("经验 +25");
            }

            EventBus<ChestOpenedEvent>.Publish(
                new ChestOpenedEvent(count, string.Join("、", names)));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            EventBus<LevelUpChoiceEvent>.Unsubscribe(OnChoice);
        }

        private void OnChoice(LevelUpChoiceEvent evt)
        {
            if (!IsChoosing || evt.ChoiceIndex < 0 || evt.ChoiceIndex >= _current.Count)
            {
                return;
            }

            Candidate chosen = _current[evt.ChoiceIndex];
            Apply(chosen);
            _experience.ConsumeLevelUp();

            IsChoosing = false;
            _current.Clear();
            EventBus<LevelUpClosedEvent>.Publish(new LevelUpClosedEvent());

            if (_experience.PendingLevelUps > 0)
            {
                OpenOffer();
            }
        }

        private void OpenOffer()
        {
            List<Candidate> pool = BuildCandidates();
            if (pool.Count == 0)
            {
                // 没有可选项时不要卡死游戏：消费掉这次升级并关闭界面。
                _experience.ConsumeLevelUp();
                IsChoosing = false;
                EventBus<LevelUpClosedEvent>.Publish(new LevelUpClosedEvent());
                return;
            }

            _current.Clear();
            var remaining = new List<Candidate>(pool);
            int optionCount = Math.Min(3, remaining.Count);

            for (int i = 0; i < optionCount; i++)
            {
                Candidate picked = PickWeighted(remaining);
                _current.Add(picked);
                remaining.RemoveAll(candidate => candidate.Id == picked.Id);
            }

            IsChoosing = true;

            var options = new UpgradeOptionInfo[_current.Count];
            for (int i = 0; i < _current.Count; i++)
            {
                Candidate candidate = _current[i];
                options[i] = new UpgradeOptionInfo(
                    candidate.Id,
                    candidate.DisplayName,
                    candidate.Description,
                    candidate.TargetLevel,
                    candidate.MaxLevel,
                    candidate.IsWeapon,
                    candidate.Icon);
            }

            EventBus<LevelUpOfferedEvent>.Publish(
                new LevelUpOfferedEvent(_experience.Level, options));
        }

        private List<Candidate> BuildCandidates()
        {
            var result = new List<Candidate>();

            if (_catalog.Weapons != null)
            {
                for (int i = 0; i < _catalog.Weapons.Length; i++)
                {
                    WeaponDefinition definition = _catalog.Weapons[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    WeaponRuntime owned = _loadout.FindWeapon(definition.Id);
                    if (owned != null)
                    {
                        if (!owned.IsMaxLevel)
                        {
                            result.Add(WeaponUpgradeCandidate(owned));
                        }
                    }
                    else if (_loadout.CanAddWeapon)
                    {
                        result.Add(NewWeaponCandidate(definition));
                    }
                }
            }

            if (_catalog.Passives != null)
            {
                for (int i = 0; i < _catalog.Passives.Length; i++)
                {
                    PassiveDefinition definition = _catalog.Passives[i];
                    if (definition == null || !definition.Offered)
                    {
                        continue;
                    }

                    PassiveRuntime owned = _loadout.FindPassive(definition.Id);
                    if (owned != null)
                    {
                        if (!owned.IsMaxLevel)
                        {
                            result.Add(PassiveUpgradeCandidate(owned));
                        }
                    }
                    else if (_loadout.CanAddPassive)
                    {
                        result.Add(NewPassiveCandidate(definition));
                    }
                }
            }

            return result;
        }

        private Candidate NewWeaponCandidate(WeaponDefinition definition)
        {
            return new Candidate(
                UpgradeKind.NewWeapon,
                definition,
                null,
                null,
                null,
                Math.Max(1, definition.Weight),
                definition.Id,
                definition.DisplayName,
                "新武器：" + definition.Description,
                1,
                definition.MaxLevel,
                definition.Icon,
                true);
        }

        private Candidate WeaponUpgradeCandidate(WeaponRuntime runtime)
        {
            WeaponDefinition definition = runtime.Definition;
            return new Candidate(
                UpgradeKind.UpgradeWeapon,
                definition,
                null,
                runtime,
                null,
                Math.Max(1, definition.Weight) * 3,
                definition.Id,
                definition.DisplayName,
                $"升级到 Lv{runtime.Level + 1}：" + definition.Description,
                runtime.Level + 1,
                definition.MaxLevel,
                definition.Icon,
                true);
        }

        private Candidate NewPassiveCandidate(PassiveDefinition definition)
        {
            return new Candidate(
                UpgradeKind.NewPassive,
                null,
                definition,
                null,
                null,
                Math.Max(1, definition.Weight),
                definition.Id,
                definition.DisplayName,
                "新被动：" + definition.Description,
                1,
                definition.MaxLevel,
                definition.Icon,
                false);
        }

        private Candidate PassiveUpgradeCandidate(PassiveRuntime runtime)
        {
            PassiveDefinition definition = runtime.Definition;
            return new Candidate(
                UpgradeKind.UpgradePassive,
                null,
                definition,
                null,
                runtime,
                Math.Max(1, definition.Weight) * 3,
                definition.Id,
                definition.DisplayName,
                $"升级到 Lv{runtime.Level + 1}：" + definition.Description,
                runtime.Level + 1,
                definition.MaxLevel,
                definition.Icon,
                false);
        }

        private Candidate PickWeighted(List<Candidate> pool)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                total += pool[i].Weight;
            }

            if (total <= 0)
            {
                return pool[0];
            }

            int roll = _random.Next(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].Weight;
                if (roll < 0)
                {
                    return pool[i];
                }
            }

            return pool[pool.Count - 1];
        }

        private void Apply(Candidate candidate)
        {
            switch (candidate.Kind)
            {
                case UpgradeKind.NewWeapon:
                    _loadout.AddWeapon(candidate.Weapon);
                    break;
                case UpgradeKind.UpgradeWeapon:
                    _loadout.UpgradeWeapon(candidate.WeaponRuntime);
                    break;
                case UpgradeKind.NewPassive:
                    _loadout.AddPassive(candidate.Passive);
                    break;
                case UpgradeKind.UpgradePassive:
                    _loadout.UpgradePassive(candidate.PassiveRuntime);
                    break;
            }
        }
    }
}
