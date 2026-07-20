using System;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Main-world inline converter that stores an entity reference in a resource.</summary>
    [Serializable]
    public class EcsEntityResourceSerializableConverter<TResource> :
        EcsEntityResourceSerializableConverter<Main, TResource>
        where TResource : struct, IEcsEntityRefResource
    {
    }
}
