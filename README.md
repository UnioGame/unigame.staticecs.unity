# UniGame Static ECS Unity

Unity integration for feature-first Static ECS composition. It provides the default `Main` world, async feature lifecycle, ScriptableObject feature factories, the service runner, conversion helpers, and editor tooling.

## Capabilities

- Ordered `StaticEcsFeatureEntry` configuration with per-entry enable state.
- Fresh runtime asset and pure feature instances created from serializable feature factories.
- Sequential UniTask registration for Update, Fixed, Late, and Cleanup groups.
- Post-initialization `StartAsync` before the runner is published or ticked.
- Explicit active-feature assembly scanning through one `RegisterAll` call.
- Transactional startup with stage/feature reporting and reverse-order rollback.
- Stable system groups exposed as `GameSys`, `FixedSys`, `LateSys`, and `CleanupSys` for `Main`.
- `EcsEntityProvider`, inline serializable converters, mono converters, converter assets, presets, and transform binding.
- A concrete service-source inspector with `Synchronize Features`.

## Usage

Keep authoring configuration and behavior in a serializable pure feature. The asset is only its Unity wrapper:

```csharp
[Serializable]
public sealed class InventoryFeature : InventoryFeature<Main>
{
    public int initialCapacity = 16;
}

[CreateAssetMenu(menuName = "Static ECS/Features/Inventory")]
public sealed class InventoryFeatureAsset : StaticEcsMainFeatureAsset<InventoryFeature> { }
```

The service clones the ScriptableObject at runtime, runs the nested pure feature, calls its
`Destroy()` method before destroying the world, and then releases the runtime asset clone.
The project asset is never used as mutable runtime state.

Features that own Update systems implement the matching async contract:

```csharp
public UniTask RegisterSystemsAsync(
    StaticEcsSystemsBuilder<Main, StaticEcsUpdateSystems> systems,
    CancellationToken cancellationToken)
{
    systems.Add(new InventorySystem(), order: 0);
    return UniTask.CompletedTask;
}
```

Add feature assets to `StaticEcsServiceSource.features` in dependency/startup order. Use `Synchronize Features` to append missing compatible assets under `Assets/`, remove null and duplicate entries, and retain the order and enabled state of existing entries.

For new entity authoring, add serializable converters to `EcsEntityProvider.serializableConverters`. They support inline configuration and can be composed into `EcsConverterPreset` assets. Mono converters remain supported for existing content and cases where a separate component is useful.

## Configuration

The feature list order controls manual type registration, async system registration, and startup. System `order` independently controls execution inside a group. Disabled feature assemblies are not scanned.

`EcsTimeFeatureAsset` and `EcsRngFeatureAsset` are ordinary entries rather than hidden service switches. A feature must validate required resources or preceding features and fail with a clear message.

Native events may first be sent from `StartAsync`, after receivers have been created by system initialization. A receiver created in `ISystem.Init` must be deleted in `Destroy` and must read, suppress, or mark all observed events as read.

For a custom world, derive from `StaticEcsFeatureAsset<TWorld,TFeature>` and
`StaticEcsServiceSource<TWorld>`. Keep `StaticEcsFeatureAsset<TWorld>` for context-dependent
factories that cannot expose a serializable pure feature. Public generic APIs in this package
also provide adjacent `Main`-default aliases.

Converter execution order remains Mono components, inline serializable converters, converter assets, then runtime registrations. A preset forwards link-resolution and destroy callbacks to its enabled nested converters. Preset assets cannot persist references to scene objects; scene-bound references should remain inline on the provider or be resolved at runtime.
