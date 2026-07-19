using System;

namespace UniGame.StaticEcs.Unity {
    [Serializable]
    public class EcsStartupReport {
        public bool worldCreated;
        public bool typesRegistered;
        public bool worldInitialized;
        public bool systemsInitialized;
        public int modulesRegistered;
        public int updateCount;
        public string message;

        public bool IsSuccess => worldCreated && typesRegistered && worldInitialized && systemsInitialized;
    }
}
