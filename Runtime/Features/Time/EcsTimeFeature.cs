using System.Threading;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Time
{
    /// <summary>Registers the ECS time resource and Unity player-loop time systems.</summary>
    public sealed class EcsTimeFeature<TWorld> :
        StaticEcsFeature<TWorld>,
        IStaticEcsSystemsFeature<TWorld, StaticEcsUpdateSystems>,
        IStaticEcsSystemsFeature<TWorld, StaticEcsFixedUpdateSystems>
        where TWorld : struct, IWorldType
    {
        /// <summary>Default order that runs time updates before gameplay systems.</summary>
        public const short DefaultUpdateOrder = short.MinValue;

        private readonly short _updateOrder;
        private readonly short _fixedOrder;
        private readonly bool _registerFixed;

        /// <summary>Creates the time feature.</summary>
        public EcsTimeFeature(
            short updateOrder = DefaultUpdateOrder,
            short fixedOrder = DefaultUpdateOrder,
            bool registerFixed = true)
        {
            _updateOrder = updateOrder;
            _fixedOrder = fixedOrder;
            _registerFixed = registerFixed;
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
            systems.Add(new EcsTimeUpdateSystem<TWorld>(), _updateOrder);
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask RegisterSystemsAsync(
            StaticEcsSystemsBuilder<TWorld, StaticEcsFixedUpdateSystems> systems,
            CancellationToken cancellationToken)
        {
            if (_registerFixed)
            {
                systems.Add(new EcsTimeFixedUpdateSystem<TWorld>(), _fixedOrder);
            }

            return UniTask.CompletedTask;
        }
    }
}
