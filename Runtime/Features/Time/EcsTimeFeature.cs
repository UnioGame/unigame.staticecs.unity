using System.Threading;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Time
{
    /// <summary>Registers the ECS time resource and Unity player-loop time systems.</summary>
    public class EcsTimeFeature<TWorld> :
        StaticEcsFeature<TWorld>,
        IStaticEcsSystemsFeature<TWorld, StaticEcsUpdateSystems>,
        IStaticEcsSystemsFeature<TWorld, StaticEcsFixedUpdateSystems>
        where TWorld : struct, IWorldType
    {
        /// <summary>Default order that runs time updates before gameplay systems.</summary>
        public const short DefaultUpdateOrder = short.MinValue;

        /// <summary>Order of the variable-step time system.</summary>
        public short updateOrder = DefaultUpdateOrder;

        /// <summary>Order of the fixed-step time system.</summary>
        public short fixedOrder = DefaultUpdateOrder;

        /// <summary>Whether the fixed-step time system is registered.</summary>
        public bool registerFixed = true;

        /// <summary>Creates the time feature.</summary>
        public EcsTimeFeature(
            short updateOrder = DefaultUpdateOrder,
            short fixedOrder = DefaultUpdateOrder,
            bool registerFixed = true)
        {
            this.updateOrder = updateOrder;
            this.fixedOrder = fixedOrder;
            this.registerFixed = registerFixed;
        }

        /// <inheritdoc />
        public override void RegisterTypes(World<TWorld>.TypeRegistrar types)
        {
            if (!World<TWorld>.HasResource<EcsTime>())
            {
                World<TWorld>.SetResource(EcsTime.Default());
            }
        }

        /// <inheritdoc />
        public UniTask RegisterSystemsAsync(
            StaticEcsSystemsBuilder<TWorld, StaticEcsUpdateSystems> systems,
            CancellationToken cancellationToken)
        {
            systems.Add(new EcsTimeUpdateSystem<TWorld>(), updateOrder);
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask RegisterSystemsAsync(
            StaticEcsSystemsBuilder<TWorld, StaticEcsFixedUpdateSystems> systems,
            CancellationToken cancellationToken)
        {
            if (registerFixed)
            {
                systems.Add(new EcsTimeFixedUpdateSystem<TWorld>(), fixedOrder);
            }

            return UniTask.CompletedTask;
        }
    }
}
