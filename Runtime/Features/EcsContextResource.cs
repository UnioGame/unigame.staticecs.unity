namespace UniGame.StaticEcs.Unity
{
    using System;
    using FFS.Libraries.StaticEcs;
    using UniGame.Core.Runtime;

    /// <summary>Exposes the non-owning application context during ECS initialization.</summary>
    public struct EcsContextResource : IResource
    {
        /// <summary>Creates a resource for a live application context.</summary>
        public EcsContextResource(IContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>Context owned by the surrounding application.</summary>
        public IContext Context;
    }

    /// <summary>Provides direct access to the application context stored in a Static ECS world.</summary>
    public static class StaticEcsContext
    {
        /// <summary>Gets the application context stored in the Main world.</summary>
        public static IContext Get()
        {
            return Get<Main>();
        }

        /// <summary>Gets the application context stored in the specified world.</summary>
        public static IContext Get<TWorld>()
            where TWorld : struct, IWorldType
        {
            return World<TWorld>.GetResource<EcsContextResource>().Context;
        }
    }
}
