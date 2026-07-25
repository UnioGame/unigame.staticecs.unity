using FFS.Libraries.StaticEcs;
using UnityTime = UnityEngine.Time;

namespace UniGame.StaticEcs.Time
{
    /// <summary>Copies Unity fixed-update timing into the ECS time resource.</summary>
    public class EcsTimeFixedUpdateSystem<TWorld> : ISystem
        where TWorld : struct, IWorldType
    {
        /// <inheritdoc />
        public void Update()
        {
            ref var time = ref World<TWorld>.GetResource<EcsTime>();
            var unscaledFixedDelta = UnityTime.fixedUnscaledDeltaTime;
            var scaledFixedDelta = unscaledFixedDelta * time.TimeScale;

            time.FixedDeltaTime = scaledFixedDelta;
            time.FixedTime += scaledFixedDelta;
        }
    }
}
