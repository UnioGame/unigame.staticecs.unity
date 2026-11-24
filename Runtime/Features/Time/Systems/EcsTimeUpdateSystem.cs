using FFS.Libraries.StaticEcs;
using UnityTime = UnityEngine.Time;

namespace UniGame.StaticEcs.Time
{
    /// <summary>Advances ECS time by the configured update step or Unity game time.</summary>
    public class EcsTimeUpdateSystem<TWorld> : ISystem
        where TWorld : struct, IWorldType
    {
        /// <inheritdoc />
        public void Update()
        {
            ref var time = ref World<TWorld>.GetResource<EcsTime>();
            var step = World<TWorld>.HasResource<EcsTimeConfig>()
                ? World<TWorld>.GetResource<EcsTimeConfig>().UpdateStepSeconds
                : null;
            var unscaledDelta = step ?? UnityTime.unscaledDeltaTime;
            var gameDelta = step ?? UnityTime.deltaTime;
            var scaledDelta = gameDelta * time.TimeScale;

            time.DeltaTime = scaledDelta;
            time.UnscaledDeltaTime = unscaledDelta;
            time.Time += scaledDelta;
            time.UnscaledTime += unscaledDelta;
            time.Now += scaledDelta;
            time.FrameCount++;
        }
    }
}
