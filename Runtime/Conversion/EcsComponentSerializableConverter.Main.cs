using System;
using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Unity
{
    /// <summary>Main-world base class for inline converters that assign one ECS component.</summary>
    [Serializable]
    public abstract class EcsComponentSerializableConverter<TComponent> :
        EcsComponentSerializableConverter<Main, TComponent>
        where TComponent : struct, IComponent
    {
    }
}
