using NUnit.Framework;
using SurvivorArena.Core;
using SurvivorArena.Core.Stats;
using SurvivorArena.Gameplay;
using UnityEngine;

namespace SurvivorArena.Tests
{
    /// <summary>
    /// PlayerMotor 的接入测试：确认移动速度确实来自 MoveSpeed Stat，
    /// 并且加修改器后一帧内立即生效（不缓存旧值）。
    /// </summary>
    public sealed class PlayerMotorTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Tick_UsesMoveSpeedStatAndReflectsModifierImmediately()
        {
            var gameObject = new GameObject("PlayerMotorTests");
            try
            {
                PlayerMotor motor = gameObject.AddComponent<PlayerMotor>();
                var input = new FakePlayerInput { Move = Vector2.right };
                var moveSpeed = new Stat(StatType.MoveSpeed, 4f, 0f);

                motor.Initialize(input, moveSpeed);
                motor.Tick(1f);
                Assert.That(gameObject.transform.position.x, Is.EqualTo(4f).Within(Tolerance));

                moveSpeed.AddModifier(StatModifier.PercentAdd("test.moveSpeed", 0.5f));
                motor.Tick(1f);
                Assert.That(gameObject.transform.position.x, Is.EqualTo(10f).Within(Tolerance));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class FakePlayerInput : IPlayerInput
        {
            public Vector2 Move { get; set; }

            public bool RestartPressed => false;

            public void Poll()
            {
            }
        }
    }
}
