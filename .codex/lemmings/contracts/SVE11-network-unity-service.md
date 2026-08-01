# SVE11 Multi-world ECS Service Contract

## Goal

Allow one Unity process to own independent Static ECS services for `Main` and additional world types without changing existing Main-world call sites.

## Public API

- `IEcsService.WorldType` exposes the exact `IWorldType` runtime type owned by the service.
- `EcsService<TWorld>.WorldType` returns `typeof(TWorld)`.
- `EcsServiceRegistry.Get<TWorld>()` returns the exact registered service or `null`.
- `EcsServiceRegistry.TryGet<TWorld>(out IEcsService service)` performs the non-throwing lookup.
- `EcsServiceRegistry.Active` remains source-compatible. It resolves the registered `Main` service first; when no `Main` service exists, it resolves the most recently registered live service.
- `EcsServiceRegistry.LastReport` remains the report from the most recent successful registration and is not cleared on unregister.

Every new public member has an English XML summary. The API stays in `UniGame.StaticEcs.Unity`; no parallel service locator is introduced.

## Registry behavior

- Registration is keyed by exact world type, not service implementation type.
- Registering the same service instance again is idempotent.
- Registering a different live service for an occupied world type throws `InvalidOperationException`; it never silently replaces the owner.
- Unregister removes only the exact registered instance. Passing `null`, an unknown service, or a stale service is a no-op.
- Registration order is deterministic and used only for the no-Main `Active` fallback.
- The registry does not own or dispose services.
- Registry mutation occurs on the Unity/service lifecycle thread. No lock or concurrent collection is added without a demonstrated cross-thread caller.

## Service lifecycle

- A service registers only after its world and all systems initialize successfully.
- Reinitializing an already registered service unregisters the old live registration before tearing down its world.
- Failed initialization and cancellation leave no registry entry for that service.
- Dispose unregisters the exact service even if cleanup of another owned resource reports an error.
- A second world registration must not replace or hide a registered `Main` service from `Active`.

## Scope

Owned implementation files are `Runtime/Service/IEcsService.cs`, `Runtime/Service/EcsService.cs`, `Runtime/Service/EcsServiceRegistry.cs`, and focused tests under `Tests/Editor/`.

No network protocol dependency, server world type, runner policy, feature asset, resource layer, reflection scanner, or game-specific code is introduced here.

## Acceptance

- Existing single-world behavior remains source-compatible.
- Focused EditMode tests cover Main priority, two non-Main world types, exact lookup, duplicate conflict, idempotent registration, stale unregister, fallback order, `LastReport`, failed reinitialization, and disposal.
- Package compiles with its existing assembly dependencies.
- `git diff --check` passes and the candidate owns only the frozen paths.
