namespace UniGame.StaticEcs.Unity.Tests
{
    using FFS.Libraries.StaticEcs;
    using NUnit.Framework;
    using UniGame.StaticEcs.Tests;
    using UniGame.StaticEcs.Time;

    public struct EcsTimeStepTestWorld : IWorldType
    {
    }

    [TestFixture]
    public class EcsTimeUpdateSystemTests
    {
        private StaticEcsTestWorld<EcsTimeStepTestWorld> _world;

        [SetUp]
        public void SetUp()
        {
            _world = new StaticEcsTestWorld<EcsTimeStepTestWorld>();
            var config = new EcsTimeConfig { UpdateStepSeconds = 0.05f };
            World<EcsTimeStepTestWorld>.SetResource(config);
            World<EcsTimeStepTestWorld>.SetResource(EcsTime.Default());
            _world.Initialize();
        }

        [TearDown]
        public void TearDown() => _world?.Dispose();

        [Test]
        public void MainStyleUpdateAdvancesTenSecondsAfterTwoHundredTicks()
        {
            var system = new EcsTimeUpdateSystem<EcsTimeStepTestWorld>();
            for (var i = 0; i < 200; i++)
                system.Update();

            var time = World<EcsTimeStepTestWorld>.GetResource<EcsTime>();
            Assert.That(time.Now, Is.EqualTo(10f).Within(0.001f));
            Assert.That(time.UnscaledTime, Is.EqualTo(10f).Within(0.001f));
            Assert.That(time.FrameCount, Is.EqualTo(200));
            Assert.That(time.DeltaTime, Is.EqualTo(0.05f));
        }

        [Test]
        public void TimeScaleAffectsScaledTimeButNotTickDuration()
        {
            ref var time = ref World<EcsTimeStepTestWorld>.GetResource<EcsTime>();
            time.SetTimeScale(0.5f);
            var system = new EcsTimeUpdateSystem<EcsTimeStepTestWorld>();
            for (var i = 0; i < 200; i++)
                system.Update();

            Assert.That(time.Now, Is.EqualTo(5f).Within(0.001f));
            Assert.That(time.UnscaledTime, Is.EqualTo(10f).Within(0.001f));
            Assert.That(time.DeltaTime, Is.EqualTo(0.025f));
        }

        [Test]
        public void ZeroScalePausesScaledTimeWhileServerTicksContinue()
        {
            ref var time = ref World<EcsTimeStepTestWorld>.GetResource<EcsTime>();
            time.SetTimeScale(0f);
            var system = new EcsTimeUpdateSystem<EcsTimeStepTestWorld>();
            for (var i = 0; i < 20; i++)
                system.Update();

            Assert.That(time.Now, Is.Zero);
            Assert.That(time.UnscaledTime, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(time.FrameCount, Is.EqualTo(20));
            Assert.That(time.DeltaTime, Is.Zero);
        }

        [Test]
        public void InvalidTickDurationIsRejected()
        {
            var config = World<EcsTimeStepTestWorld>.GetResource<EcsTimeConfig>();
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                config.UpdateStepSeconds = 0f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                config.UpdateStepSeconds = float.NaN);
        }

        [Test]
        public void UnconfiguredUpdateUsesUnityGameTime()
        {
            World<EcsTimeStepTestWorld>.GetResource<EcsTimeConfig>().UpdateStepSeconds = null;
            var expectedGameDelta = UnityEngine.Time.deltaTime;
            var expectedUnscaledDelta = UnityEngine.Time.unscaledDeltaTime;
            new EcsTimeUpdateSystem<EcsTimeStepTestWorld>().Update();
            var time = World<EcsTimeStepTestWorld>.GetResource<EcsTime>();
            Assert.That(time.DeltaTime, Is.EqualTo(expectedGameDelta));
            Assert.That(time.Now, Is.EqualTo(expectedGameDelta));
            Assert.That(time.UnscaledDeltaTime, Is.EqualTo(expectedUnscaledDelta));
        }
    }
}
