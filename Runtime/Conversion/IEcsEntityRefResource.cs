using FFS.Libraries.StaticEcs;

namespace UniGame.StaticEcs.Unity {
    public interface IEcsEntityRefResource : IResource {
        EntityGID Gid { get; set; }
    }
}
