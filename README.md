# Static ECS Unity

## Capabilities

This package owns the default `Main` world, feature assets, Unity authoring,
converters, presets, player-loop runners, automatic type discovery, asynchronous
resource access, centralized provider authoring, rollback, and repeated initialization.

Startup runs in this order:

1. terminate the previous world lifetime, then destroy its systems, world, and runtime assets;
2. clone enabled feature assets and collect their asset and programmatic feature
   inheritance assemblies;
3. create the world and publish `EcsWorldLifeTimeResource`, `EcsContextResource`,
   `StaticEcsWorldConfig`, and system configuration;
4. create enabled system groups;
5. call `RegisterAll` for the collected enabled feature assemblies;
6. run assembly registrars for closed generic ECS types;
7. initialize features using the configured parallel or sequential mode;
8. initialize the world and system groups;
9. start the runner.

Any exception or cancellation records the feature and stage and rolls the
partially created world back.

## Usage

Use a serialized programmatic feature for normal gameplay composition:

```csharp
[Serializable]
public sealed class DemoFeature : StaticEcsFeature<Main>
{
    public override async UniTask InitializeAsync(ILifeTime lifeTime)
    {
        World<Main>.Resource<MatchRegistry> matches = default;
        await matches.GetAsync(lifeTime);

        var configuration = new DemoConfiguration();

        World<Main>.SetResource(configuration);
        lifeTime.AddDispose(CreateSubscription());
        World<Main>.Systems<StaticEcsUpdateSystems>.Add(
            new DemoSystem(),
            100);
    }
}

public sealed class DemoFeatureAsset :
    StaticEcsMainFeatureAsset<DemoFeature>
{
}
```

A feature implemented entirely by a ScriptableObject can use the standalone
base:

```csharp
public sealed class SceneFeatureAsset : StaticEcsFeatureAsset
{
    protected override UniTask OnInitializeAsync(ILifeTime lifeTime)
    {
        var configuration = new SceneConfiguration();
        World<Main>.SetResource(configuration);
        return UniTask.CompletedTask;
    }
}
```

`EcsService` automatically scans concrete value types in enabled feature
assemblies:

| Marker | Automatic registration |
|---|---|
| `IComponent` | component |
| `ITag` | tag |
| `IEvent` | event |
| `ILinkType` | `Link<T>` |
| `ILinksType` | `Links<T>` |
| `IMultiComponent` | `Multi<T>` |
| `IEntityType` | entity type |

It does not register open generic definitions, required closed generic
constructions, `IResource`, systems, groups, feature assets, converters,
abstract types, unmarked classes, or assemblies belonging only to disabled
features.

For `StaticEcsFeatureAsset<TWorld, TFeature>`, discovery includes the assembly
of the asset plus the assemblies that define `TFeature` and its programmatic
feature base classes. A feature variant can therefore inherit shared markers
from another runtime asmdef without adding a second feature asset or manually
registering ordinary components.

Declare closed generic types once in their owning assembly:

```csharp
[assembly: StaticEcsTypeRegistrar(typeof(DemoClosedTypes))]

internal sealed class DemoClosedTypes : IStaticEcsTypeRegistrar<Main>
{
    public void Register(World<Main>.TypeRegistrar types)
    {
        types.Event<GameActionEvent<HealAction>>();
    }
}
```

Access the application context in one line during initialization:

```csharp
var context = StaticEcsContext.Get();
var customWorldContext = StaticEcsContext.Get<TWorld>();
```

Runtime code should read the typed ECS Resources that initialization publishes,
not query the context on every tick.

### Centralized entity authoring

`EcsEntityProvider<TWorld>` keeps the normal serialized converter surface while delegating
ECS mutation to `EcsAuthoringRegistry<TWorld>`. `Awake` and `Start` register creation intent
according to `UsageType`; `CreateEntity()` explicitly registers the same intent and does not
promise that an entity already exists.

`EcsService<TWorld>` performs an initial drain after world and system initialization and a
drain before every Update, FixedUpdate, LateUpdate, and Cleanup boundary, even when that
system group is disabled. Providers created inside a group wait for the next invoked
boundary. Requests may therefore arrive before startup, from additive scenes, or from
runtime prefab instances without a scene scan.

A normal batch is ordered by scene path, hierarchy sibling path, and provider component
index. Dependency-ready providers are created in waves; links and created callbacks run
only after the batch has been created. Any converter, link, or callback exception rolls back
every entity created by that batch and clears its provider GID.

Trusted factories that cannot wait for a service boundary may use the atomic immediate path:

```csharp
if (EcsAuthoringRegistry.TryCreateImmediate(provider, out var gid, out var reason))
{
    // Add trusted server-owned data before exposing the GameObject to gameplay.
}
```

`TryCreateImmediate` uses the same converter and link lifecycle but intentionally does not
participate in scene-batch ordering. Service teardown first stops new authoring drains, lets
systems release runtime state that refers to authored entities, and then destroys provider-owned
entities before the world. Persistent enabled intent is retained for repeated startup.

Feature initialization receives the lifetime owned by the current world directly.
Pass the same instance to nested programmatic features and use
`lifeTime.Token` only for asynchronous operations that support cancellation.

Code outside feature initialization can access the lifetime through its handle:

```csharp
var lifeTime = World<Main>.Handle.GetLifeTime();
var customWorldLifeTime = World<TWorld>.Handle.GetLifeTime();
```

The same instance is available through the resource API:

```csharp
var lifeTime = World<TWorld>
    .GetResource<EcsWorldLifeTimeResource>()
    .LifeTime;
```

Use the world lifetime for initialization-owned subscriptions, background
operations, and `IDisposable` objects. Systems continue to own their runtime
state through `Init` and `Destroy`.

## Configuration

`StaticEcsWorldConfig.featureInitializationMode` defaults to
`StaticEcsFeatureInitializationMode.Parallel`. Parallel mode overlaps asynchronous
feature pipelines with `UniTask.WhenAll`; it does not move feature code to worker
threads. This allows a feature to wait for a resource published by a later configured
feature. Completion order is not deterministic, so resource overrides or other
order-sensitive composition must select
`StaticEcsFeatureInitializationMode.Sequential`.

`StaticEcsWorldConfig.editorDependencyTimeoutMs` defaults to 5 seconds.
`playerDependencyTimeoutMs` defaults to 10 seconds. Lifetime cancellation remains
distinct from resource timeout. A timeout reports the exact resource requested
at the call site.

For optional asynchronous access:

```csharp
World<TWorld>.Resource<MatchRegistry> matches = default;
var registry = await matches.GetAsync(lifeTime);
```

The polling callback uses UniTask's state overload and a static lambda. Resource
structs are returned as copies; resource classes are returned as references.

`EcsContextResource` does not own or dispose `IContext`. Repeated initialization
terminates the old world lifetime before destroying systems, the world, and
runtime feature clones. Resource removal does not call `IDisposable.Dispose`;
register initialization-owned disposables with the world lifetime instead.
Disabled converters are skipped during apply, link resolution, and destroy
callbacks.
