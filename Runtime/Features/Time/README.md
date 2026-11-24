# ECS Time Feature

## Capabilities

`EcsTimeFeature<TWorld>` publishes one `EcsTime` resource per world and advances it
before gameplay Update systems. `EcsTimeFeature` is the `Main` world alias.
Consumers read `EcsTime` from their own world; the resource does not synchronize
time between worlds or replicate it over the network.

| `EcsTime` field | Meaning |
| --- | --- |
| `DeltaTime` | Scaled duration of the latest ECS Update. Use for gameplay timers, effects, and regeneration. |
| `Now`, `Time` | Accumulated scaled Update time. Both advance by `DeltaTime`. |
| `UnscaledDeltaTime`, `UnscaledTime` | Duration and accumulated time before `EcsTime.TimeScale` is applied. |
| `FrameCount` | Number of ECS Update calls, not necessarily Unity rendered frames. |
| `FixedDeltaTime`, `FixedTime` | Scaled FixedUpdate duration and its separate accumulated time. |
| `TimeScale` | Additional per-world scale; defaults to `1`. |

## Usage

Add the feature to each world that needs time, before features that consume it:

```csharp
serverComposition.Add<EcsTimeFeature>();
clientComposition.Add<EcsTimeFeature<ClientWorld>>();
```

In a system, read the resource from the system's world:

```csharp
var deltaTime = World<TWorld>.GetResource<EcsTime>().DeltaTime;
```

Set a network-tick duration for a world whose Update runs once per simulation
tick. Do this after the feature initializes and before the first world Update:

```csharp
var timeConfig = World<Main>.GetResource<EcsTimeConfig>();
timeConfig.UpdateStepSeconds = simulationClock.TickSeconds;
```

The game Host and dedicated Server configure `Main` this way from
`NetworkSimulationConfigResource.TickSeconds`. At 20 Hz, 200 Updates advance
`EcsTime.Now` by about 10 seconds with the default `TimeScale`, even when Unity
renders more or fewer frames. The game `ClientWorld` leaves the step unset and
updates once per client frame.

Pause or scale a world's gameplay time through its resource:

```csharp
ref var time = ref World<Main>.GetResource<EcsTime>();
time.SetTimeScale(0f);   // pause scaled time
time.SetTimeScale(1f);   // resume normal speed
```

At scale `0`, `DeltaTime`, `Now`, and `Time` stop advancing. `FrameCount` and
unscaled time continue. `SetTimeScale` clamps negative values to zero.

For timer and cooldown setup, see the [game timer guide](../../../../../../docs/guides/timers.md).

## Configuration

`EcsTimeConfig.UpdateStepSeconds` is optional. A positive finite value is used
for both scaled and unscaled Update fields before `EcsTime.TimeScale` is applied.
Zero, negative, `NaN`, and infinity are rejected. When it is `null`:

- Scaled Update fields use `UnityEngine.Time.deltaTime * EcsTime.TimeScale`.
- Unscaled Update fields use `UnityEngine.Time.unscaledDeltaTime`.

Thus a client using Unity's frame time is also affected by Unity
`Time.timeScale`; `EcsTime.TimeScale` is an additional world-specific scale.
A tick-configured world uses the configured step instead of Unity frame time.

`UpdateOrder` defaults to `short.MinValue` so time advances before gameplay
systems. The feature registers its FixedUpdate system only when the world has
a FixedUpdate group and `RegisterFixed` is true. FixedUpdate always uses
`UnityEngine.Time.fixedUnscaledDeltaTime * EcsTime.TimeScale`; the optional
Update step does not change it.
