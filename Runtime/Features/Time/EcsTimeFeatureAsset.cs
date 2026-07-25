namespace UniGame.StaticEcs.Time
{
    using Unity;
    using UnityEngine;

    /// <summary>Creates an isolated Main-world ECS time feature.</summary>
    [CreateAssetMenu(menuName = "Static ECS/Features/Time", fileName = nameof(EcsTimeFeatureAsset))]
    public sealed class EcsTimeFeatureAsset :
        StaticEcsMainFeatureAsset<EcsTimeFeature>
    {
    }
}
