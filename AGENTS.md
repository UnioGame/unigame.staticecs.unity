# AGENTS — unigame.staticecs.unity

Use the repo-local `$build-static-ecs-features` skill for feature assets, converter authoring, presets, lifecycle changes, migrations, and reviews in this package.

## Layer

- This package owns Unity integration, UniTask lifecycle, feature assets, bootstrap, conversion, the runner, and the default `Main` world.
- It may depend on `unigame.staticecs`; it must not depend on gameplay feature assemblies.
- Every new public generic-on-world class has a neighboring `<TypeName>.Main.cs` alias. Add Main-default overloads for public static generic operations.

## Feature composition

- ECS service sources store only ordered `StaticEcsFeatureEntry` ScriptableObject references. Do not add module APIs or inline `SerializeReference` features.
- A feature asset is a factory and never a mutable runtime feature instance. Serializable assets use `StaticEcsFeatureAsset<TWorld,TFeature>`; the service clones the asset and runs its pure nested feature.
- Feature `Destroy()` runs before world destruction. The service destroys the runtime asset clone only after the feature lifecycle and world cleanup complete.
- Preserve configuration order for type registration, async system registration, and startup. System order is a separate concern.
- Await feature registration sequentially. Publish the service and start the runner only after all groups initialize and every `StartAsync` completes.
- On failure, destroy created groups in reverse order, then pure runtime features, the world, and runtime asset clones. Normal disposal uses the same order.
- Scan assemblies only for active feature assets/runtime instances; keep closed generics and resources explicitly registered.
- `Synchronize Features` searches only under `Assets/`, retains existing order/enabled state, removes null/duplicate entries, and supports Undo.

## Systems and events

- Systems may be structs or classes. Implement only used `ISystem` lifecycle methods.
- Native-event receivers are created in `Init`, consumed or suppressed, and deleted in `Destroy`. First startup events belong in `StartAsync`, not system registration.

## Converter authoring

- Prefer `EcsComponentSerializableConverter` / `EcsSerializableConverter` in `serializableConverters` over new `EcsMonoConverter` components.
- Use `EcsMonoConverter` only when independent MonoBehaviour identity/lifecycle, runtime attachment, or unavoidable scene integration is required.
- Use `EcsConverterPreset` assets for repeated recipes and entities created from configuration. Presets are immutable authoring data and forward link/destroy lifecycle to enabled nested converters.
- Keep shared build logic in utilities when serializable, Mono, and asset adapters coexist.

## Documentation

- Public docs are English, use Capabilities / Usage / Configuration, and every public API has an XML summary.
