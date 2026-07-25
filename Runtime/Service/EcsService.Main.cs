namespace UniGame.StaticEcs.Unity
{
    /// <summary>Default-world ECS service.</summary>
    public sealed class EcsService : EcsService<Main>
    {
        /// <summary>Creates an uninitialized default-world ECS service.</summary>
        public EcsService(StaticEcsWorldConfig worldConfig, StaticEcsSystemsConfig systemsConfig)
            : base(worldConfig, systemsConfig) { }
    }
}
