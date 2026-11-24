# AGENTS — unigame.staticecs.unity

Use the repo-local `$build-static-ecs-features` skill for feature assets, converter authoring, presets, lifecycle changes, migrations, and reviews in this package.

## Layer

- This package owns Unity integration, UniTask lifecycle, feature assets, bootstrap, conversion, the runner, and the default `Main` world.
- It may depend on `unigame.staticecs`; it must not depend on gameplay feature assemblies.
- Every new public generic-on-world class has a neighboring `<TypeName>.Main.cs` alias. Add Main-default overloads for public static generic operations.

## Feature composition

- A world is composed from ordered `StaticEcsComposition<TWorld>` declarations:
  code features through `Add<TFeature>()` and feature assets through `Add(asset)` or
  `AddEntries`. ECS service source assets store ordered `StaticEcsFeatureEntry`
  references. Do not add module APIs or inline `SerializeReference` features.
- A fresh code feature instance or a fresh asset clone is the runtime initialization
  boundary for every initialization. Code features are parameterless. A programmatic
  feature type is declared once per world; a duplicate throws from `Add`.
- Feature assets and code declarations are equally supported ways to compose a world
  and may be mixed in one composition. Neither is legacy; choose per feature.
- Data that several worlds must keep identical (for example ability and item configuration)
  belongs in one ScriptableObject `IResource` published with `composition.AddResource(asset)`,
  not in a copy per world-bound feature asset. Features read it with the resource API.
- Use `StaticEcsFeatureAsset<TWorld, TFeature>` for a serialized programmatic feature.
  Derive from `StaticEcsFeatureAsset<TWorld>` directly only when the ScriptableObject
  itself owns the implementation.
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
- Scan assemblies only for declared features. Include the asset assembly of a cloned
  asset and the assemblies of the programmatic feature type and its base classes,
  stopping before the shared framework base. This keeps cross-asmdef feature variants
  self-contained without scanning disabled assets. `RegisterAll` owns ordinary concrete markers; assembly registrars own
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

- Public docs are English and use Capabilities / Usage / Configuration. XML summaries
  and comments follow the repository-wide rules.
