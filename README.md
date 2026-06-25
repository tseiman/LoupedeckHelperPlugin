# LoupedeckHelperPlugin

Minimal Loupedeck plugin used to isolate loader issues.

Current contents:

- one plugin class
- one local `MultiWheel Fn` toggle action
- local read-only IPC provider for `get` and `watch`
- small SDK-independent client library
- package metadata

The IPC provider writes discovery to:

- macOS: `~/Library/Application Support/LoupedeckSharedState/shared-state.json`
- Linux: `~/.local/share/LoupedeckSharedState/shared-state.json`
- Windows: `%LOCALAPPDATA%\LoupedeckSharedState\shared-state.json`

The socket protocol is newline-delimited JSON:

```json
{"cmd":"get","key":"loupedeck.shared.multiwheel.keep-active"}
{"cmd":"watch","key":"loupedeck.shared.multiwheel.keep-active"}
```

Build:

```sh
dotnet build LoupedeckHelperPlugin.sln -c Debug
```

Client usage from another plugin:

```xml
<ProjectReference Include="..\..\..\LoupedeckHelperPlugin\src\LoupedeckSharedStateClient\LoupedeckSharedStateClient.csproj" />
```

```csharp
using Loupedeck.SharedState;

var client = new LoupedeckSharedStateClient();
var keepActive = client.TryGetMultiWheelKeepActive(out var value) && value;
```

CLI test client:

```sh
dotnet run --project tools/LoupedeckSharedStateCli
```

The CLI prints the current state once, then keeps running and prints every
state change broadcast by the helper. If the helper plugin is not running, it
prints `MultiWheel Fn: OFF (provider unavailable)` and exits with code `1`.
