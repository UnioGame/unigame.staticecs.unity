namespace UniGame.StaticEcs.Unity {
    public static class EcsServiceRegistry {
        public static IEcsService Active { get; private set; }

        public static EcsStartupReport LastReport { get; private set; }

        public static void Register(IEcsService service) {
            Active = service;
            LastReport = service?.Report;
        }

        public static void Unregister(IEcsService service) {
            if (!ReferenceEquals(Active, service)) {
                return;
            }

            Active = null;
        }
    }
}
