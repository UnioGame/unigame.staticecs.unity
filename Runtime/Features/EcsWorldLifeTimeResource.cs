namespace UniGame.StaticEcs.Unity
{
    using System;
    using FFS.Libraries.StaticEcs;
    using UniGame.Core.Runtime;

    /// <summary>Exposes the lifetime owned by the current Static ECS world instance.</summary>
    public sealed class EcsWorldLifeTimeResource : IResource
    {
        /// <summary>Creates a non-owning resource for a live world lifetime.</summary>
        public EcsWorldLifeTimeResource(ILifeTime lifeTime)
        {
            LifeTime = lifeTime ?? throw new ArgumentNullException(nameof(lifeTime));
        }

        /// <summary>Gets the lifetime terminated when the current world is torn down.</summary>
        public ILifeTime LifeTime { get; }
    }

    /// <summary>Provides direct lifetime access for active Static ECS world handles.</summary>
    public static class StaticEcsWorldLifeTimeExtensions
    {
        /// <summary>Gets the lifetime resource published for the supplied active world.</summary>
        public static ILifeTime GetLifeTime(this ref WorldHandle world)
        {
            if (world.WorldType == null)
                throw new InvalidOperationException(
                    "Static ECS world lifetime is unavailable because the world is not active.");

            var resourceType = typeof(EcsWorldLifeTimeResource);
            if (!world.HasResource(resourceType))
                throw new InvalidOperationException(
                    $"Static ECS world `{world.WorldType.FullName}` has no " +
                    $"`{nameof(EcsWorldLifeTimeResource)}`. " +
                    "The lifetime is available only after EcsService bootstrap.");

            return ((EcsWorldLifeTimeResource)world.GetResource(resourceType)).LifeTime;
        }
    }
}
