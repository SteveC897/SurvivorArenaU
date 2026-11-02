using System;
using System.Collections.Generic;
using SurvivorArena.Core;
using SurvivorArena.Core.Stats;
using SurvivorArena.Data;
using UnityEngine;

namespace SurvivorArena.Gameplay
{
    /// <summary>
    /// 玩家背包（Gameplay 层）：6 武器槽 + 6 被动槽。
    /// 负责武器等级、被动修改器注册，以及把槽位变化通过 EventBus 发给 HUD。
    /// 被动每件只用一条 modifier（SourceId = passive.&lt;id&gt;），升级时 SetModifierValue。
    /// </summary>
    public sealed class Loadout
    {
        private readonly PlayerStats _stats;
        private readonly List<WeaponRuntime> _weapons = new List<WeaponRuntime>();
        private readonly List<PassiveRuntime> _passives = new List<PassiveRuntime>();

        public Loadout(PlayerStats stats, int maxWeapons, int maxPassives)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            MaxWeapons = Mathf.Max(1, maxWeapons);
            MaxPassives = Mathf.Max(1, maxPassives);
        }

        public int MaxWeapons { get; }

        public int MaxPassives { get; }

        public IReadOnlyList<WeaponRuntime> Weapons => _weapons;

        public IReadOnlyList<PassiveRuntime> Passives => _passives;

        public bool CanAddWeapon => _weapons.Count < MaxWeapons;

        public bool CanAddPassive => _passives.Count < MaxPassives;

        public WeaponRuntime FindWeapon(string id)
        {
            for (int i = 0; i < _weapons.Count; i++)
            {
                if (_weapons[i].Definition.Id == id)
                {
                    return _weapons[i];
                }
            }

            return null;
        }

        public PassiveRuntime FindPassive(string id)
        {
            for (int i = 0; i < _passives.Count; i++)
            {
                if (_passives[i].Definition.Id == id)
                {
                    return _passives[i];
                }
            }

            return null;
        }

        public WeaponRuntime AddWeapon(WeaponDefinition definition)
        {
            if (definition == null || !CanAddWeapon || FindWeapon(definition.Id) != null)
            {
                return null;
            }

            var runtime = new WeaponRuntime(definition);
            _weapons.Add(runtime);
            PublishChanged();
            return runtime;
        }

        public bool UpgradeWeapon(WeaponRuntime runtime)
        {
            if (runtime == null || !_weapons.Contains(runtime) || !runtime.TryUpgrade())
            {
                return false;
            }

            PublishChanged();
            return true;
        }

        public PassiveRuntime AddPassive(PassiveDefinition definition)
        {
            if (definition == null || !CanAddPassive || FindPassive(definition.Id) != null)
            {
                return null;
            }

            var runtime = new PassiveRuntime(definition);
            _passives.Add(runtime);
            ApplyPassive(runtime);
            PublishChanged();
            return runtime;
        }

        public bool UpgradePassive(PassiveRuntime runtime)
        {
            if (runtime == null || !_passives.Contains(runtime) || !runtime.TryUpgrade())
            {
                return false;
            }

            ApplyPassive(runtime);
            PublishChanged();
            return true;
        }

        public void PublishChanged()
        {
            EventBus<LoadoutChangedEvent>.Publish(new LoadoutChangedEvent(
                BuildSlots(_weapons, true),
                BuildSlots(_passives, false)));
        }

        private void ApplyPassive(PassiveRuntime runtime)
        {
            PassiveDefinition definition = runtime.Definition;
            Stat stat = _stats.Get(MapStat(definition.Effect));
            StatModifierType type = MapModifier(definition.ModifierKind);
            float value = definition.GetValue(runtime.Level);
            string sourceId = "passive." + definition.Id;

            if (!runtime.Applied)
            {
                stat.AddModifier(new StatModifier(sourceId, type, value));
                runtime.Applied = true;
            }
            else
            {
                stat.SetModifierValue(sourceId, value);
            }
        }

        private static StatType MapStat(PassiveEffect effect)
        {
            switch (effect)
            {
                case PassiveEffect.Might:
                    return StatType.Might;
                case PassiveEffect.Cooldown:
                    return StatType.Cooldown;
                case PassiveEffect.Area:
                    return StatType.Area;
                case PassiveEffect.ProjectileSpeed:
                    return StatType.ProjectileSpeed;
                case PassiveEffect.Duration:
                    return StatType.Duration;
                case PassiveEffect.Amount:
                    return StatType.Amount;
                case PassiveEffect.Magnet:
                    return StatType.Magnet;
                case PassiveEffect.Armor:
                    return StatType.Armor;
                case PassiveEffect.MaxHealth:
                    return StatType.MaxHealth;
                case PassiveEffect.Recovery:
                    return StatType.Recovery;
                default:
                    return StatType.Might;
            }
        }

        private static StatModifierType MapModifier(PassiveModifierKind kind)
        {
            switch (kind)
            {
                case PassiveModifierKind.FlatAdd:
                    return StatModifierType.FlatAdd;
                case PassiveModifierKind.PercentMul:
                    return StatModifierType.PercentMul;
                default:
                    return StatModifierType.PercentAdd;
            }
        }

        private static LoadoutSlotInfo[] BuildSlots(IReadOnlyList<WeaponRuntime> weapons, bool isWeapon)
        {
            var slots = new LoadoutSlotInfo[weapons.Count];
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponRuntime weapon = weapons[i];
                slots[i] = new LoadoutSlotInfo(
                    weapon.Definition.Id,
                    weapon.Definition.DisplayName,
                    weapon.Definition.Icon,
                    weapon.Level,
                    weapon.Definition.MaxLevel,
                    true);
            }

            return slots;
        }

        private static LoadoutSlotInfo[] BuildSlots(IReadOnlyList<PassiveRuntime> passives, bool isWeapon)
        {
            var slots = new LoadoutSlotInfo[passives.Count];
            for (int i = 0; i < passives.Count; i++)
            {
                PassiveRuntime passive = passives[i];
                slots[i] = new LoadoutSlotInfo(
                    passive.Definition.Id,
                    passive.Definition.DisplayName,
                    passive.Definition.Icon,
                    passive.Level,
                    passive.Definition.MaxLevel,
                    false);
            }

            return slots;
        }
    }
}
