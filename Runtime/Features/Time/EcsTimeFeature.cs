using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;
using UniGame.Core.Runtime;

namespace UniGame.StaticEcs.Time
{
    /// <summary>Registers the ECS time resource and Unity player-loop time systems.</summary>
    public class EcsTimeFeature<TWorld> : StaticEcsFeature<TWorld>
        where TWorld : struct, IWorldType
    {
        /// <summary>Default order that runs time updates before gameplay systems.</summary>
        public const short DefaultUpdateOrder = short.MinValue;

        /// <summary>Execution order of the Update time system.</summary>
        public short updateOrder = DefaultUpdateOrder;

        /// <summary>Execution order of the FixedUpdate time system.</summary>
        public short fixedOrder = DefaultUpdateOrder;

        /// <summary>Whether the FixedUpdate time system is installed.</summary>
        public bool registerFixed = true;

        /// <inheritdoc />
        public override UniTask InitializeAsync(ILifeTime lifeTime)
        {
            if (!World<TWorld>.HasResource<EcsTimeConfig>())
            {
                var configuration = new EcsTimeConfig
                {
                    UpdateOrder = updateOrder,
                    FixedOrder = fixedOrder,
                    RegisterFixed = registerFixed,
                };

                World<TWorld>.SetResource(configuration);
            }

            if (!World<TWorld>.HasResource<EcsTime>())
            {
                var time = EcsTime.Default();
                World<TWorld>.SetResource(time);
            }

            var updateEnabled =
                World<TWorld>.HasResource<Unity.StaticEcsSystemsConfig>() &&
                World<TWorld>.GetResource<Unity.StaticEcsSystemsConfig>().update;
            var fixedEnabled =
                World<TWorld>.HasResource<Unity.StaticEcsSystemsConfig>() &&
                World<TWorld>.GetResource<Unity.StaticEcsSystemsConfig>().fixedUpdate;
            ref var config = ref World<TWorld>.GetResource<EcsTimeConfig>();
            if (updateEnabled)
            {
                World<TWorld>.Systems<StaticEcsUpdateSystems>.Add(
                    new EcsTimeUpdateSystem<TWorld>(),
                    config.UpdateOrder);
            }

            if (fixedEnabled && config.RegisterFixed)
            {
                World<TWorld>.Systems<StaticEcsFixedUpdateSystems>.Add(
                    new EcsTimeFixedUpdateSystem<TWorld>(),
                    config.FixedOrder);
            }

            return UniTask.CompletedTask;
        }
    }

    /// <summary>Controls ECS time system composition and execution order.</summary>
    public sealed class EcsTimeConfig : IResource
    {
        /// <summary>Execution order of the Update time system.</summary>
        public short UpdateOrder = short.MinValue;

        /// <summary>Execution order of the FixedUpdate time system.</summary>
        public short FixedOrder = short.MinValue;

        /// <summary>Whether the FixedUpdate time system is installed.</summary>
        public bool RegisterFixed = true;
    }
}
