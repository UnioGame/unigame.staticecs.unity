using UnityEngine;

namespace UniGame.StaticEcs.Random
{
    using Unity;

    /// <summary>Creates an isolated Main-world random resource feature.</summary>
    [CreateAssetMenu(menuName = "Static ECS/Features/Random", fileName = nameof(EcsRngFeatureAsset))]
    public sealed class EcsRngFeatureAsset : StaticEcsMainFeatureAsset<EcsRngFeature> { }
}
