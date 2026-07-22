using System;

namespace UniGame.StaticEcs.Random
{
    using Unity;

    /// <summary>Main-world ECS random resource feature.</summary>
    [Serializable]
    public sealed class EcsRngFeature : EcsRngFeature<Main>
    {
        /// <summary>Creates a time-seeded Main-world random feature.</summary>
        public EcsRngFeature() { }

        /// <summary>Creates a deterministically seeded Main-world random feature.</summary>
        public EcsRngFeature(uint seed)
            : base(seed) { }
    }
}
