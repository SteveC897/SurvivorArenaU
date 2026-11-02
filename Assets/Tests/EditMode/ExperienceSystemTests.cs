using NUnit.Framework;
using SurvivorArena.Core;
using SurvivorArena.Gameplay;
using UnityEngine;

namespace SurvivorArena.Tests
{
    public sealed class ExperienceSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus<XpGemCollectedEvent>.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<XpGemCollectedEvent>.Clear();
        }

        [Test]
        public void Curve_MatchesDocumentedEarlyLevels()
        {
            Assert.That(ExperienceSystem.GetXpToNext(1), Is.EqualTo(5));
            Assert.That(ExperienceSystem.GetXpToNext(2), Is.EqualTo(15));
            Assert.That(ExperienceSystem.GetXpToNext(19), Is.EqualTo(185));
            Assert.That(ExperienceSystem.GetXpToNext(20), Is.EqualTo(785));
            Assert.That(ExperienceSystem.GetXpToNext(21), Is.EqualTo(798));
        }

        [Test]
        public void AddXp_LevelsUpAndQueuesOneChoicePerLevel()
        {
            var experience = new ExperienceSystem();
            try
            {
                experience.AddXp(4);
                Assert.That(experience.Level, Is.EqualTo(1));
                Assert.That(experience.Xp, Is.EqualTo(4));
                Assert.That(experience.PendingLevelUps, Is.EqualTo(0));

                experience.AddXp(1);
                Assert.That(experience.Level, Is.EqualTo(2));
                Assert.That(experience.Xp, Is.EqualTo(0));
                Assert.That(experience.PendingLevelUps, Is.EqualTo(1));

                experience.AddXp(15);
                Assert.That(experience.Level, Is.EqualTo(3));
                Assert.That(experience.PendingLevelUps, Is.EqualTo(2));

                experience.ConsumeLevelUp();
                Assert.That(experience.PendingLevelUps, Is.EqualTo(1));
            }
            finally
            {
                experience.Dispose();
            }
        }

        [Test]
        public void CollectedGemEvent_AddsXp()
        {
            var experience = new ExperienceSystem();
            try
            {
                EventBus<XpGemCollectedEvent>.Publish(new XpGemCollectedEvent(7, Vector2.zero));
                Assert.That(experience.Level, Is.EqualTo(2));
                Assert.That(experience.Xp, Is.EqualTo(2));
                Assert.That(experience.PendingLevelUps, Is.EqualTo(1));
            }
            finally
            {
                experience.Dispose();
            }
        }
    }
}
