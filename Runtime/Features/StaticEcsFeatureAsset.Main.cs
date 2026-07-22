namespace UniGame.StaticEcs.Unity
{
    /// <summary>Main-world feature asset factory.</summary>
    public abstract class StaticEcsFeatureAsset : StaticEcsFeatureAsset<Main>
    {
    }

    /// <summary>Main-world asset that exposes a serializable pure feature.</summary>
    public abstract class StaticEcsMainFeatureAsset<TFeature> :
        StaticEcsFeatureAsset<Main, TFeature>
        where TFeature : class, IStaticEcsFeature<Main>, new()
    {
    }
}
