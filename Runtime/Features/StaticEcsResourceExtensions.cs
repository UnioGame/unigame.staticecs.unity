namespace UniGame.StaticEcs.Unity
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using FFS.Libraries.StaticEcs;
    using UniGame.Core.Runtime;

    /// <summary>Provides asynchronous initialization helpers for Static ECS resources.</summary>
    public static class StaticEcsResourceExtensions
    {
        /// <summary>Returns a resource or waits for it using the current world configuration and lifetime.</summary>
        public static UniTask<TResource> GetAsync<TWorld, TResource>(
            this World<TWorld>.Resource<TResource> resource,
            ILifeTime lifeTime)
            where TWorld : struct, IWorldType
            where TResource : IResource
        {
            var timeout = World<TWorld>.HasResource<StaticEcsWorldConfig>()
                ? World<TWorld>.GetResource<StaticEcsWorldConfig>().GetDependencyTimeout()
                : StaticEcsWorldConfig.Default.GetDependencyTimeout();

            return resource.GetAsync(timeout, lifeTime.Token);
        }

        /// <summary>Returns a resource immediately or waits until it is registered.</summary>
        public static async UniTask<TResource> GetAsync<TWorld, TResource>(
            this World<TWorld>.Resource<TResource> resource,
            TimeSpan timeout,
            CancellationToken cancellationToken)
            where TWorld : struct, IWorldType
            where TResource : IResource
        {
            if (resource.IsRegistered)
                return resource.Value;

            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var timeoutTimer = timeoutCancellation.CancelAfterSlim(timeout);
            try
            {
                await UniTask.WaitWhile(
                    resource,
                    static resourceHandle => !resourceHandle.IsRegistered,
                    cancellationToken: timeoutCancellation.Token);
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Static ECS resource `{typeof(TResource).FullName}` was not resolved " +
                    $"within {timeout.TotalMilliseconds} ms.",
                    exception);
            }

            return resource.Value;
        }
    }
}
