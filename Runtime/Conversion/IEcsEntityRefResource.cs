using FFS.Libraries.StaticEcs;

namespace unigame.staticecs.unity {
    public interface IEcsEntityRefResource : IResource {
        EntityGID Gid { get; set; }
    }
}
