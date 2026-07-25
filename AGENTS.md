# AGENTS — unigame.staticecs.unity

Use the repo-local `$build-static-ecs-features` skill for feature assets, converter authoring, presets, lifecycle changes, migrations, and reviews in this package.

## Layer

- This package owns Unity integration, UniTask lifecycle, feature assets, bootstrap, conversion, the runner, and the default `Main` world.
- It may depend on `unigame.staticecs`; it must not depend on gameplay feature assemblies.
- Every new public generic-on-world class has a neighboring `<TypeName>.Main.cs` alias. Add Main-default overloads for public static generic operations.

## Feature composition

- ECS service sources store only ordered `StaticEcsFeatureEntry` ScriptableObject references. Do not add module APIs or inline `SerializeReference` features.
- A feature asset is cloned by the service and is the runtime initialization boundary.
  Use `StaticEcsFeatureAsset<TWorld, TFeature>` for a serialized programmatic
  feature. Derive from `StaticEcsFeatureAsset<TWorld>` directly only when the
  ScriptableObject itself owns the implementation.
- Programmatic features read asynchronous dependencies directly through
  `Resource<T>.GetAsync(lifeTime)`. Do not add dependency descriptors, installers,
  validation contexts, or manual `new Feature().InitializeAsync(...)` adapters.
- At feature registration boundaries, write every type, resource, system, handler, and registry registration as a separate statement. Do not use fluent registration chains.
- Construct every resource in a named local before `SetResource`. Keep object
  initialization and resource publication as separate statements.
- Start feature pipelines in configured order. By default their asynchronous dependency
  resolution and initialization may overlap; use sequential mode only when composition
  intentionally depends on completion order. System order is a separate concern.
- Publish bootstrap resources, auto-register active feature assemblies, run closed-generic
  assembly registrars, then initialize features using the configured mode.
- On failure, destroy created groups, the world, and runtime asset clones. Normal disposal uses the same ownership order.
- Scan assemblies only for enabled runtime-cloned feature assets. For a generic
  adapter, include the asset assembly and the assemblies of `TFeature` and its
  programmatic feature base classes, stopping before the shared framework base.
  This keeps cross-asmdef feature variants self-contained without scanning disabled
  assets. `RegisterAll` owns ordinary concrete markers; assembly registrars own
  required closed generic constructions. Resources never require type registration.
- `EcsContextResource` is non-owning. Access it through `StaticEcsContext.Get()` only at initialization boundaries; realtime code consumes typed ECS Resources.
- `EcsWorldLifeTimeResource` is a non-owning view of the lifetime owned by the
  current world instance. Feature initialization receives this same instance directly;
  use `World<TWorld>.Handle.GetLifeTime()` outside that boundary. Repeated initialization,
  rollback, and service disposal terminate it before system and world teardown.
- `Synchronize Features` searches only under `Assets/`, retains existing order/enabled state, removes null/duplicate entries, and supports Undo.

## Systems and events

- Production systems are public and each lives in its own file under the owning
  feature's `Systems/` directory. Do not declare systems inside feature assets or
  configuration files.
- Systems may be structs or classes. Implement only used `ISystem` lifecycle methods.
- Native-event receivers are created in `Init`, consumed or suppressed, and deleted in `Destroy`. Startup events that require receivers belong in an explicitly ordered initialization system.

## Converter authoring

- Prefer `EcsComponentSerializableConverter` / `EcsSerializableConverter` in `serializableConverters` over new `EcsMonoConverter` components.
- Use `EcsMonoConverter` only when independent MonoBehaviour identity/lifecycle, runtime attachment, or unavoidable scene integration is required.
- Use `EcsConverterPreset` assets for repeated recipes and entities created from configuration. Presets are immutable authoring data and forward link/destroy lifecycle to enabled nested converters.
- Keep shared build logic in utilities when serializable, Mono, and asset adapters coexist.

## Documentation

- Public docs are English, use Capabilities / Usage / Configuration, and every public API has an XML summary.
