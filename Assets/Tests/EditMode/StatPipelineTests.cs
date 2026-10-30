using System.Collections.Generic;
using NUnit.Framework;
using SurvivorArena.Core.Stats;

namespace SurvivorArena.Tests
{
    /// <summary>
    /// 属性修改器管线的 EditMode 测试（不依赖场景/Unity 对象）：
    /// 计算顺序、去重、移除、脏标记、震荡重算、下限钳制、StatBlock。
    /// </summary>
    public sealed class StatPipelineTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Recalculate_AppliesFlatThenPercentAddThenPercentMul()
        {
            var stat = new Stat(StatType.Might, 100f);
            stat.AddModifier(StatModifier.Flat("test.flat", 20f));
            stat.AddModifier(StatModifier.PercentAdd("test.percentAdd", 0.5f));
            stat.AddModifier(StatModifier.PercentMul("test.percentMul", 0.5f));

            // (100 + 20) * (1 + 0.5) * (1 + 0.5) = 270
            Assert.That(stat.Value, Is.EqualTo(270f).Within(Tolerance));
        }

        [Test]
        public void AddModifier_SameId_ReplacesInsteadOfStacking()
        {
            var stat = new Stat(StatType.Might, 100f);
            stat.AddModifier(StatModifier.Flat("passive.hollowHeart", 10f, "passive.hollowHeart.flat"));
            Assert.That(stat.Value, Is.EqualTo(110f).Within(Tolerance));

            stat.AddModifier(StatModifier.Flat("passive.hollowHeart", 25f, "passive.hollowHeart.flat"));

            Assert.That(stat.Value, Is.EqualTo(125f).Within(Tolerance));
            Assert.That(stat.Modifiers.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemoveModifier_RestoresBaseValue()
        {
            var stat = new Stat(StatType.Might, 100f);
            stat.AddModifier(StatModifier.PercentAdd("passive.test", 0.5f));
            Assert.That(stat.Value, Is.EqualTo(150f).Within(Tolerance));

            bool removed = stat.RemoveModifier("passive.test");

            Assert.That(removed, Is.True);
            Assert.That(stat.Value, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void RemoveAllModifiersFromSource_RemovesOnlyThatSource()
        {
            var stat = new Stat(StatType.Might, 100f);
            stat.AddModifier(StatModifier.Flat("passive.hollowHeart", 10f, "passive.hollowHeart.flat"));
            stat.AddModifier(StatModifier.PercentAdd("passive.hollowHeart", 0.2f, "passive.hollowHeart.percent"));
            stat.AddModifier(StatModifier.Flat("arcana.XVI", 5f));

            // (100 + 10 + 5) * 1.2 = 138
            Assert.That(stat.Value, Is.EqualTo(138f).Within(Tolerance));

            int removed = stat.RemoveAllModifiersFromSource("passive.hollowHeart");

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(stat.Value, Is.EqualTo(105f).Within(Tolerance));
            Assert.That(stat.Modifiers.Count, Is.EqualTo(1));
        }

        [Test]
        public void Oscillation_SetModifierValue_RecomputesFromBaseWithoutDrift()
        {
            var stat = new Stat(StatType.Area, 100f);
            stat.AddModifier(StatModifier.PercentAdd("arcana.V", 0f, "arcana.V"));

            float[] phases = { 0.2f, -0.5f, 0.5f, 0f };
            float[] expected = { 120f, 50f, 150f, 100f };

            for (int i = 0; i < phases.Length; i++)
            {
                Assert.That(stat.SetModifierValue("arcana.V", phases[i]), Is.True);
                Assert.That(stat.Value, Is.EqualTo(expected[i]).Within(Tolerance));
            }

            // 回到 0 相位后必须精确回到 base，不能有逐帧乘算的累积误差。
            Assert.That(stat.Value, Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void DirtyFlag_RecalculatesOnceUntilChanged()
        {
            var stat = new Stat(StatType.Might, 10f);

            Assert.That(stat.Value, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(stat.RecalculationCount, Is.EqualTo(1));

            _ = stat.Value;
            Assert.That(stat.RecalculationCount, Is.EqualTo(1));

            stat.AddModifier(StatModifier.Flat("test.flat", 5f));
            stat.AddModifier(StatModifier.PercentAdd("test.percentAdd", 0.1f));

            Assert.That(stat.Value, Is.EqualTo(16.5f).Within(Tolerance));
            Assert.That(stat.RecalculationCount, Is.EqualTo(2));
        }

        [Test]
        public void SetBaseValue_RecomputesWithExistingModifiers()
        {
            var stat = new Stat(StatType.Might, 10f);
            stat.AddModifier(StatModifier.Flat("test.flat", 5f));
            Assert.That(stat.Value, Is.EqualTo(15f).Within(Tolerance));

            stat.SetBaseValue(20f);

            Assert.That(stat.Value, Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void MinValue_ClampsResult()
        {
            var stat = new Stat(StatType.Cooldown, 1f, 0.1f);
            stat.AddModifier(StatModifier.Flat("test.flat", -5f));

            Assert.That(stat.Value, Is.EqualTo(0.1f).Within(Tolerance));
        }

        [Test]
        public void PercentMul_ModifiersMultiply()
        {
            var stat = new Stat(StatType.Might, 100f);
            stat.AddModifier(StatModifier.PercentMul("test.mulA", 0.5f));
            stat.AddModifier(StatModifier.PercentMul("test.mulB", 0.5f));

            Assert.That(stat.Value, Is.EqualTo(225f).Within(Tolerance));
        }

        [Test]
        public void Changed_EventFiresOnlyWhenValueActuallyChanges()
        {
            var stat = new Stat(StatType.Might, 10f);
            _ = stat.Value;

            int changes = 0;
            stat.Changed += _ => changes++;

            stat.AddModifier(StatModifier.Flat("test.flat", 5f));
            Assert.That(changes, Is.EqualTo(0));

            _ = stat.Value;
            Assert.That(changes, Is.EqualTo(1));

            _ = stat.Value;
            Assert.That(changes, Is.EqualTo(1));

            stat.RemoveModifier("test.flat");
            _ = stat.Value;
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void StatBlock_GetTryGetAndMissing()
        {
            var block = new StatBlock(new[]
            {
                new Stat(StatType.Might, 1f),
                new Stat(StatType.MaxHealth, 100f)
            });

            Assert.That(block.Count, Is.EqualTo(2));
            Assert.That(block.Get(StatType.Might), Is.Not.Null);
            Assert.That(block.TryGet(StatType.MaxHealth, out Stat health), Is.True);
            Assert.That(health.BaseValue, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(block.TryGet(StatType.Luck, out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => block.Get(StatType.Luck));
        }

        [Test]
        public void Modifier_RejectsInvalidValues()
        {
            Assert.Throws<System.ArgumentException>(() => new StatModifier(" ", StatModifierType.FlatAdd, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new StatModifier("test", StatModifierType.FlatAdd, float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new Stat(StatType.Might, float.PositiveInfinity));
        }
    }
}
