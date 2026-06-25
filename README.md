# LoupedeckHelperPlugin

Helper plugin for shared local Loupedeck state that needs to be visible across
multiple isolated plugins.

The first action is `MultiWheel Fn`. It owns the shared state
`loupedeck.shared.multiwheel.keep-active`:

- `false`: compatible MultiWheel actions may return to their default display.
- `true`: compatible MultiWheel actions stay active after their command completes.

The plugin exposes the state over local IPC only:

- macOS/Linux: Unix domain socket
- Windows: named pipe
- no HTTP endpoint

## Projects

- `src/LoupedeckHelperPlugin`: Loupedeck plugin and `MultiWheel Fn` action.
- `src/LoupedeckSharedStateLib`: reusable client/discovery library for ATEM, JON and other plugins.
- `tools/LoupedeckSharedStateTestClient`: small console client for manual IPC checks.

## Discovery

On startup the helper writes `shared-state.json`:

- macOS: `~/Library/Application Support/LoupedeckSharedState/shared-state.json`
- Linux: `~/.local/share/LoupedeckSharedState/shared-state.json`
- Windows: `%LOCALAPPDATA%\LoupedeckSharedState\shared-state.json`

Example:

```json
{
  "version": 1,
  "provider": "LoupedeckSharedStatePlugin",
  "capabilities": [
    "loupedeck.shared.multiwheel.keep-active"
  ],
  "endpoint": "unix:/Users/tseiman/Library/Application Support/LoupedeckSharedState/loupedeck-shared-state.sock"
}
```

The file is removed on plugin unload where possible. Clients treat missing or
stale discovery as unavailable and fall back to `false`.

## Protocol

The socket protocol is newline-delimited JSON.

```json
{"id":"1","cmd":"get","key":"loupedeck.shared.multiwheel.keep-active"}
{"id":"2","cmd":"set","key":"loupedeck.shared.multiwheel.keep-active","value":true}
{"id":"3","cmd":"toggle","key":"loupedeck.shared.multiwheel.keep-active"}
{"id":"4","cmd":"disable","key":"loupedeck.shared.multiwheel.keep-active"}
{"id":"5","cmd":"subscribe","key":"loupedeck.shared.multiwheel.keep-active"}
```

Responses look like:

```json
{"id":"1","ok":true,"value":false}
```

Subscribers receive:

```json
{"event":"changed","key":"loupedeck.shared.multiwheel.keep-active","value":true}
```

## Build

Requires .NET 8 and the Logi/Loupedeck Plugin Service SDK files.

```sh
dotnet build LoupedeckHelperPlugin.sln -c Debug
```

The helper plugin currently builds with IPC disabled by default to isolate
Loupedeck loader issues. To include the socket provider again, build the plugin
project with:

```sh
dotnet build src/LoupedeckHelperPlugin/LoupedeckHelperPlugin.csproj -c Debug -p:EnableSharedStateIpc=true
```

The plugin expects `PluginApi.dll` at the standard SDK locations used by the
reference plugins:

- Windows: `C:\Program Files\Logi\LogiPluginService\PluginApi.dll`
- macOS: `/Applications/Utilities/LogiPluginService.app/Contents/MonoBundle/PluginApi.dll`

Linux builds are useful for the shared library and test client. A full plugin
build needs the Loupedeck SDK binaries.

## Test Client

When the helper plugin is running:

```sh
dotnet run --project tools/LoupedeckSharedStateTestClient -- get
dotnet run --project tools/LoupedeckSharedStateTestClient -- set true
dotnet run --project tools/LoupedeckSharedStateTestClient -- toggle
dotnet run --project tools/LoupedeckSharedStateTestClient -- disable
dotnet run --project tools/LoupedeckSharedStateTestClient -- watch
```

If the helper is not running, clients fail quickly and should use `false`.

## Diagnostics

In addition to the Loupedeck plugin log, the plugin writes an early startup
diagnostic log here on macOS:

```text
~/Library/Application Support/LoupedeckSharedState/helper-plugin-debug.log
```

If that path cannot be created, it falls back to:

```text
/tmp/helper-plugin-debug.log
```

This file logs constructor, command initialization, `Load()`, IPC startup, socket
paths, discovery writes, assembly resolution requests and unhandled exceptions.

## Client Integration

Reference `src/LoupedeckSharedStateLib/LoupedeckSharedStateLib.csproj` from a
compatible plugin and keep the MultiWheel decision local. The detailed
integration guide is in [docs/client-integration.md](docs/client-integration.md).

```csharp
using Loupedeck.SharedState;

private readonly LoupedeckSharedStateClient _sharedState = new();

var keepActive = this._sharedState.TryGetMultiWheelKeepActive(out var value) && value;
if (!keepActive)
{
    multiWheelFnState.Disable();
}
```

For UI refreshes, clients can subscribe:

```csharp
this._sharedState.MultiWheelKeepActiveChanged += value => this.ActionImageChanged();
await this._sharedState.StartWatchingAsync();
```
