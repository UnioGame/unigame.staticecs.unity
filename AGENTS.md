# AGENTS — unigame.staticecs.unity

## Layer

- This package owns Unity integration, UniTask lifecycle, feature assets, bootstrap, conversion, the runner, and the default `Main` world.
- It may depend on `unigame.staticecs`; it must not depend on gameplay feature assemblies.
- Every new public generic-on-world class has a neighboring `<TypeName>.Main.cs` alias. Add Main-default overloads for public static generic operations.

## Feature composition

- ECS service sources store only ordered `StaticEcsFeatureEntry` ScriptableObject references. Do not add module APIs or inline `SerializeReference` features.
- A feature asset is a factory and never a mutable runtime feature instance.
- Preserve configuration order for type registration, async system registration, and startup. System order is a separate concern.
- Await feature registration sequentially. Publish the service and start the runner only after all groups initialize and every `StartAsync` completes.
- On failure, destroy created groups in reverse order, then the world and runtime features. Normal disposal also destroys groups before the world.
- Scan assemblies only for active feature assets/runtime instances; keep closed generics and resources explicitly registered.
- `Synchronize Features` searches only under `Assets/`, retains existing order/enabled state, removes null/duplicate entries, and supports Undo.

## Systems and events

- Systems may be structs or classes. Implement only used `ISystem` lifecycle methods.
- Native-event receivers are created in `Init`, consumed or suppressed, and deleted in `Destroy`. First startup events belong in `StartAsync`, not system registration.

## Documentation

- Public docs are English, use Capabilities / Usage / Configuration, and every public API has an XML summary.
