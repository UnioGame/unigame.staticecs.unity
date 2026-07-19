using UniGame.Core.Runtime;
using UnityEngine;

namespace UniGame.StaticEcs.Random
{
    using Unity;

    /// <summary>Creates an isolated Main-world random resource feature.</summary>
    [CreateAssetMenu(menuName = "Static ECS/Features/Random", fileName = nameof(EcsRngFeatureAsset))]
    public sealed class EcsRngFeatureAsset : StaticEcsFeatureAsset
    {
        /// <summary>Uses the serialized seed instead of a time-derived seed.</summary>
        public bool useFixedSeed;
        /// <summary>Deterministic seed used when <see cref="useFixedSeed"/> is enabled.</summary>
        public uint seed = 1;

        /// <inheritdoc />
        public override IStaticEcsFeature<Main> CreateFeature(IContext context)
        {
            return useFixedSeed ? new EcsRngFeature<Main>(seed) : new EcsRngFeature<Main>();
        }
    }
}
