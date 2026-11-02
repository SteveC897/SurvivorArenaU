using NUnit.Framework;
using SurvivorArena.Core;
using SurvivorArena.Data;
using SurvivorArena.Gameplay;
using UnityEngine;

namespace SurvivorArena.Tests
{
    public sealed class UpgradeSystemTests
    {
        private BattleConfig _config;
        private ContentCatalog _catalog;
        private PlayerStats _stats;
        private Loadout _loadout;
        private ExperienceSystem _experience;
        private UpgradeSystem _upgrades;
        private UpgradeOptionInfo[] _offered;
        private int _chestEvents;

        [SetUp]
        public void SetUp()
        {
            EventBus<LevelUpOfferedEvent>.Clear();
            EventBus<LevelUpChoiceEvent>.Clear();
            EventBus<LevelUpClosedEvent>.Clear();
            EventBus<ChestOpenedEvent>.Clear();

            _config = ScriptableObject.CreateInstance<BattleConfig>();
            _catalog = ScriptableObject.CreateInstance<ContentCatalog>();
            _stats = new PlayerStats(_config);
            _loadout = new Loadout(_stats, 6, 6);
            _experience = new ExperienceSystem();
            _upgrades = new UpgradeSystem(_stats, _loadout, _experience, _catalog, 12345);

            EventBus<LevelUpOfferedEvent>.Subscribe(OnOffered);
            EventBus<ChestOpenedEvent>.Subscribe(OnChestOpened);
        }

        [TearDown]
        public void TearDown()
        {
            _upgrades.Dispose();
            _experience.Dispose();
            EventBus<LevelUpOfferedEvent>.Unsubscribe(OnOffered);
            EventBus<ChestOpenedEvent>.Unsubscribe(OnChestOpened);
            EventBus<LevelUpOfferedEvent>.Clear();
            EventBus<LevelUpChoiceEvent>.Clear();
            EventBus<LevelUpClosedEvent>.Clear();
            EventBus<ChestOpenedEvent>.Clear();

            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Offer_ContainsOwnedUpgradeAndNewPassive_AndChoiceApplies()
        {
            WeaponDefinition wand = MakeWeapon("wand", 2);
            PassiveDefinition might = MakeMightPassive("might", 2, true);
            _catalog.SetContent(new[] { wand }, new[] { might }, null, null);
            _loadout.AddWeapon(wand);

            _experience.AddXp(5);
            _upgrades.Tick();

            Assert.That(_upgrades.IsChoosing, Is.True);
            Assert.That(_offered, Is.Not.Null);
            Assert.That(_offered.Length, Is.EqualTo(2));
            Assert.That(_offered[0].Id, Is.Not.EqualTo(_offered[1].Id));

            int index = FindOption("might");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));

            EventBus<LevelUpChoiceEvent>.Publish(new LevelUpChoiceEvent(index));

            Assert.That(_experience.PendingLevelUps, Is.EqualTo(0));
            Assert.That(_upgrades.IsChoosing, Is.False);
            Assert.That(_loadout.FindPassive("might"), Is.Not.Null);
            Assert.That(_stats.Might.Value, Is.EqualTo(1.1f).Within(0.0001f));
        }

        [Test]
        public void WeaponUpgrade_LevelsUpSameRuntime()
        {
            WeaponDefinition wand = MakeWeapon("wand", 2);
            _catalog.SetContent(new[] { wand }, new PassiveDefinition[0], null, null);
            _loadout.AddWeapon(wand);

            _experience.AddXp(5);
            _upgrades.Tick();

            Assert.That(_offered.Length, Is.EqualTo(1));
            Assert.That(_offered[0].Id, Is.EqualTo("wand"));
            Assert.That(_offered[0].Level, Is.EqualTo(2));

            EventBus<LevelUpChoiceEvent>.Publish(new LevelUpChoiceEvent(0));

            Assert.That(_loadout.Weapons[0].Level, Is.EqualTo(2));
            Assert.That(_experience.PendingLevelUps, Is.EqualTo(0));
        }

        [Test]
        public void MaxedOwnedWeapon_OnlyOffersNewPassive()
        {
            WeaponDefinition wand = MakeWeapon("wand", 1);
            PassiveDefinition might = MakeMightPassive("might", 2, true);
            _catalog.SetContent(new[] { wand }, new[] { might }, null, null);
            _loadout.AddWeapon(wand);

            _experience.AddXp(5);
            _upgrades.Tick();

            Assert.That(_offered.Length, Is.EqualTo(1));
            Assert.That(_offered[0].Id, Is.EqualTo("might"));

            EventBus<LevelUpChoiceEvent>.Publish(new LevelUpChoiceEvent(0));

            Assert.That(_loadout.FindPassive("might"), Is.Not.Null);
            Assert.That(_stats.Might.Value, Is.EqualTo(1.1f).Within(0.0001f));
        }

        [Test]
        public void ChestRewards_ApplyMultipleUpgradesWithoutLevelUpChoice()
        {
            WeaponDefinition wand = MakeWeapon("wand", 1);
            PassiveDefinition might = MakeMightPassive("might", 2, true);
            _catalog.SetContent(new[] { wand }, new[] { might }, null, null);
            _loadout.AddWeapon(wand);

            _upgrades.GrantChestRewards(2);

            PassiveRuntime passive = _loadout.FindPassive("might");
            Assert.That(passive, Is.Not.Null);
            Assert.That(passive.Level, Is.EqualTo(2));
            Assert.That(_stats.Might.Value, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(_chestEvents, Is.EqualTo(1));
            Assert.That(_experience.PendingLevelUps, Is.EqualTo(0));
            Assert.That(_upgrades.IsChoosing, Is.False);
        }

        [Test]
        public void Offer_DoesNotRepeatTheSameItem()
        {
            WeaponDefinition wand = MakeWeapon("wand", 2);
            WeaponDefinition knife = MakeWeapon("knife", 2);
            PassiveDefinition might = MakeMightPassive("might", 2, true);
            PassiveDefinition area = MakeAreaPassive("area", 2);
            _catalog.SetContent(
                new[] { wand, knife },
                new[] { might, area },
                null,
                null);
            _loadout.AddWeapon(wand);

            _experience.AddXp(5);
            _upgrades.Tick();

            Assert.That(_offered.Length, Is.EqualTo(3));
            for (int i = 0; i < _offered.Length; i++)
            {
                for (int j = i + 1; j < _offered.Length; j++)
                {
                    Assert.That(_offered[i].Id, Is.Not.EqualTo(_offered[j].Id));
                }
            }
        }

        private int FindOption(string id)
        {
            if (_offered == null)
            {
                return -1;
            }

            for (int i = 0; i < _offered.Length; i++)
            {
                if (_offered[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnOffered(LevelUpOfferedEvent evt)
        {
            _offered = evt.Options;
        }

        private void OnChestOpened(ChestOpenedEvent evt)
        {
            _chestEvents++;
        }

        private static WeaponDefinition MakeWeapon(string id, int maxLevel)
        {
            var levels = new WeaponLevelStats[maxLevel];
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = new WeaponLevelStats(10f, 1f, 1f, 1f, 0f, 1, 1);
            }

            return new WeaponDefinition
            {
                Id = id,
                DisplayName = id,
                Description = "test weapon",
                Behavior = WeaponBehavior.MagicWand,
                MaxLevel = maxLevel,
                Weight = 100,
                Levels = levels
            };
        }

        private static PassiveDefinition MakeMightPassive(string id, int maxLevel, bool offered)
        {
            var values = new float[maxLevel];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 0.1f * (i + 1);
            }

            return new PassiveDefinition
            {
                Id = id,
                DisplayName = id,
                Description = "test passive",
                MaxLevel = maxLevel,
                Weight = 100,
                Effect = PassiveEffect.Might,
                ModifierKind = PassiveModifierKind.PercentAdd,
                LevelValues = values,
                Offered = offered
            };
        }

        private static PassiveDefinition MakeAreaPassive(string id, int maxLevel)
        {
            var values = new float[maxLevel];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 0.1f * (i + 1);
            }

            return new PassiveDefinition
            {
                Id = id,
                DisplayName = id,
                Description = "test passive",
                MaxLevel = maxLevel,
                Weight = 100,
                Effect = PassiveEffect.Area,
                ModifierKind = PassiveModifierKind.PercentAdd,
                LevelValues = values,
                Offered = true
            };
        }
    }
}
