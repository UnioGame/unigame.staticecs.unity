namespace UniGame.StaticEcs.Unity
{
    using System;
    using FFS.Libraries.StaticEcs;

    /// <summary>Associates an assembly with a registrar for closed generic ECS types.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class StaticEcsTypeRegistrarAttribute : Attribute
    {
        /// <summary>Creates an assembly registrar declaration.</summary>
        public StaticEcsTypeRegistrarAttribute(Type registrarType)
        {
            RegistrarType = registrarType ??
                throw new ArgumentNullException(nameof(registrarType));
        }

        /// <summary>Gets the concrete registrar type.</summary>
        public Type RegistrarType { get; }
    }

    /// <summary>Registers closed generic ECS types for one world.</summary>
    public interface IStaticEcsTypeRegistrar<TWorld>
        where TWorld : struct, IWorldType
    {
        /// <summary>Registers the assembly-owned closed generic ECS types.</summary>
        void Register(World<TWorld>.TypeRegistrar types);
    }

    /// <summary>
    /// Registers configuration-derived ECS types owned by a runtime feature asset before
    /// the world is initialized.
    /// </summary>
    public interface IStaticEcsFeatureTypeRegistrar<TWorld>
        where TWorld : struct, IWorldType
    {
        /// <summary>Registers the feature-owned types required by its serialized configuration.</summary>
        void RegisterTypes(World<TWorld>.TypeRegistrar types);
    }
}
