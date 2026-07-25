namespace UniGame.StaticEcs.Unity.Tests.DisabledSupport
{
    using Cysharp.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using UniGame.Core.Runtime;
    using UniGame.StaticEcs;

    /// <summary>Component owned by a programmatic feature base in this support assembly.</summary>
    public struct ProgrammaticFeatureBaseComponent : IComponent { }

    /// <summary>World used to verify cross-assembly programmatic feature discovery.</summary>
    public struct ProgrammaticFeatureWorld : IWorldType { }

    /// <summary>Programmatic base used to verify cross-assembly feature type discovery.</summary>
    public class ProgrammaticFeatureBase :
        StaticEcsFeature<ProgrammaticFeatureWorld>
    {
        /// <inheritdoc />
        public override UniTask InitializeAsync(ILifeTime lifeTime)
        {
            return UniTask.CompletedTask;
        }
    }

    /// <summary>Component used to verify that disabled feature assemblies are not auto-scanned.</summary>
    public struct DisabledAutoComponent : IComponent { }

    /// <summary>Feature asset intentionally disabled by the registration test.</summary>
    public sealed class DisabledFeatureAsset : StaticEcsFeatureAsset
    {
        protected override UniTask OnInitializeAsync(ILifeTime lifeTime)
        {
            var resource = new DisabledFeatureInitializedResource();
            World<Main>.SetResource(resource);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>Signals that the disabled feature was incorrectly initialized.</summary>
    public struct DisabledFeatureInitializedResource : IResource { }
}
