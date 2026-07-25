using FFS.Libraries.StaticEcs;
using UnityTime = UnityEngine.Time;

namespace UniGame.StaticEcs.Time
{
    /// <summary>Copies Unity update timing into the ECS time resource.</summary>
    public class EcsTimeUpdateSystem<TWorld> : ISystem
        where TWorld : struct, IWorldType
    {
        /// <inheritdoc />
        public void Update()
        {
            ref var time = ref World<TWorld>.GetResource<EcsTime>();
            var unscaledDelta = UnityTime.unscaledDeltaTime;
            var scaledDelta = unscaledDelta * time.TimeScale;

            time.DeltaTime = scaledDelta;
            time.UnscaledDeltaTime = unscaledDelta;
            time.Time += scaledDelta;
            time.UnscaledTime += unscaledDelta;
            time.Now += scaledDelta;
            time.FrameCount++;
        }
    }
}
