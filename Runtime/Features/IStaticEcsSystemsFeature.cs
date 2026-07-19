using System.Threading;
using Cysharp.Threading.Tasks;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs
{
    /// <summary>Registers systems asynchronously into one isolated Static ECS systems group.</summary>
    public interface IStaticEcsSystemsFeature<TWorld, TSystemsType> : IStaticEcsFeature<TWorld>
        where TWorld : struct, IWorldType
        where TSystemsType : struct, ISystemsType
    {
        /// <summary>Initializes dependencies and adds systems before the group is initialized.</summary>
        UniTask RegisterSystemsAsync(
            StaticEcsSystemsBuilder<TWorld, TSystemsType> systems,
            CancellationToken cancellationToken);
    }

    /// <summary>Runs feature startup after all systems groups have been initialized.</summary>
    public interface IStaticEcsStartupFeature<TWorld> : IStaticEcsFeature<TWorld>
        where TWorld : struct, IWorldType
    {
        /// <summary>Starts the feature before the ECS runner begins ticking.</summary>
        UniTask StartAsync(CancellationToken cancellationToken);
    }
}
