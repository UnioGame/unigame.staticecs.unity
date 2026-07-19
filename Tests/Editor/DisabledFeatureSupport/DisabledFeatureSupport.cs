using FFS.Libraries.StaticEcs;
using UniGame.Core.Runtime;

namespace UniGame.StaticEcs.Unity.Tests.DisabledSupport
{
    /// <summary>Component used to verify that disabled feature assemblies are not auto-scanned.</summary>
    public struct DisabledAutoComponent : IComponent { }

    /// <summary>Component used to verify that disabled features do not run manual registration.</summary>
    public struct DisabledManualComponent : IComponent { }

    /// <summary>Feature asset intentionally disabled by the registration test.</summary>
    public sealed class DisabledFeatureAsset : StaticEcsFeatureAsset
    {
        /// <summary>Creates the isolated test feature.</summary>
        public override IStaticEcsFeature<Main> CreateFeature(IContext context) => new DisabledFeature();
    }

    internal sealed class DisabledFeature : StaticEcsFeature<Main>
    {
        public override void RegisterTypes(World<Main>.TypeRegistrar types)
        {
            types.Component<DisabledManualComponent>();
        }
    }
}
