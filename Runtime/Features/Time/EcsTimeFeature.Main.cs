using System;

namespace UniGame.StaticEcs.Time
{
    using Unity;

    /// <summary>Main-world ECS time feature.</summary>
    [Serializable]
    public sealed class EcsTimeFeature : EcsTimeFeature<Main>
    {
        /// <summary>Creates the Main-world time feature with default configuration.</summary>
        public EcsTimeFeature() { }

        /// <summary>Creates the Main-world time feature.</summary>
        public EcsTimeFeature(
            short updateOrder = DefaultUpdateOrder,
            short fixedOrder = DefaultUpdateOrder,
            bool registerFixed = true)
            : base(updateOrder, fixedOrder, registerFixed) { }
    }
}
