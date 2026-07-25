namespace UniGame.StaticEcs.Unity
{
    using UnityEngine;

    /// <summary>Default-world Static ECS service source asset.</summary>
    [CreateAssetMenu(menuName = "Static ECS/Service Source", fileName = nameof(StaticEcsServiceSource))]
    public sealed class StaticEcsServiceSource : StaticEcsServiceSource<Main> { }
}
