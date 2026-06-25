# LoupedeckHelperPlugin

Minimal Loupedeck plugin used to isolate loader issues.

Current contents:

- one plugin class
- one local `MultiWheel Fn` toggle action
- local IPC provider for `get`, `set`, `toggle`, and `disable`
- small SDK-independent client library
- package metadata

The IPC provider writes discovery to:

- macOS: `~/Library/Application Support/LoupedeckSharedState/shared-state.json`
- Linux: `~/.local/share/LoupedeckSharedState/shared-state.json`
- Windows: `%LOCALAPPDATA%\LoupedeckSharedState\shared-state.json`

The socket protocol is newline-delimited JSON:

```json
{"id":"1","cmd":"get","key":"loupedeck.shared.multiwheel.keep-active"}
{"id":"2","cmd":"set","key":"loupedeck.shared.multiwheel.keep-active","value":true}
{"id":"3","cmd":"toggle","key":"loupedeck.shared.multiwheel.keep-active"}
{"id":"4","cmd":"disable","key":"loupedeck.shared.multiwheel.keep-active"}
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
