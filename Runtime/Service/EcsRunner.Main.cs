namespace UniGame.StaticEcs.Unity
{
    /// <summary>Default-world player-loop runner.</summary>
    public sealed class EcsRunner : EcsRunner<Main>
    {
        /// <summary>Creates a runner for the supplied default-world service.</summary>
        public EcsRunner(EcsService<Main> service, StaticEcsSystemsConfig config)
            : base(service, config) { }
    }
}
