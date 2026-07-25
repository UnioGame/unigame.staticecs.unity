namespace UniGame.StaticEcs.Unity
{
    /// <summary>Standalone Main-world feature asset.</summary>
    public abstract class StaticEcsFeatureAsset : StaticEcsFeatureAsset<Main>
    {
    }

    /// <summary>Main-world asset adapter for a serialized programmatic feature.</summary>
    public abstract class StaticEcsMainFeatureAsset<TFeature> :
        StaticEcsFeatureAsset<Main, TFeature>
        where TFeature : class, IStaticEcsFeature<Main>, new()
    {
    }
}
