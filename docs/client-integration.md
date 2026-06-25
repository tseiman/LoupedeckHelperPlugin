# Client Integration

Yes. Other Loupedeck plugins can reference `LoupedeckSharedStateLib` and use it
without linking against the helper plugin assembly itself.

The library is intentionally SDK-independent. It only uses .NET APIs, reads the
helper discovery file, and talks to the local Unix domain socket or Windows
named pipe. If the helper plugin is missing or not running, reads return
`false` quickly and writes become no-ops.

## Recommended Layout

For the current sibling repository layout:

```text
agent-work/
  LoupedeckHelperPlugin/
  LoupedeckAtemControlerPlugin/
  JONImageProcessorLoupeControl/
```

add a `ProjectReference` from the client plugin project to:

```text
../../../LoupedeckHelperPlugin/src/LoupedeckSharedStateLib/LoupedeckSharedStateLib.csproj
```

This relative path is correct for both:

```text
LoupedeckAtemControlerPlugin/src/LoupedeckAtemControlerPlugin/LoupedeckAtemControlerPlugin.csproj
JONImageProcessorLoupeControl/src/JONImageProcessorLoupeControlPlugin/JONImageProcessorLoupeControlPlugin.csproj
```

## Project File

Add this property and project reference to the client plugin `.csproj`:

```xml
<PropertyGroup>
  <SharedStateLibProject>../../../LoupedeckHelperPlugin/src/LoupedeckSharedStateLib/LoupedeckSharedStateLib.csproj</SharedStateLibProject>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="$(SharedStateLibProject)" />
</ItemGroup>

<Target Name="VerifySharedStateLibReference" BeforeTargets="ResolveReferences" Condition="!Exists('$(MSBuildProjectDirectory)/$(SharedStateLibProject)')">
  <Error Text="LoupedeckSharedStateLib project not found. Expected: $(SharedStateLibProject)" />
</Target>
```

If the helper repository is checked out somewhere else, change only
`SharedStateLibProject`.

## Basic Use

Create one client instance per plugin-level service or adapter, not one per
button press:

```csharp
using Loupedeck.SharedState;

internal sealed class SharedMultiWheelFnState : IDisposable
{
    private readonly LoupedeckSharedStateClient _client = new();

    public Boolean IsEnabled
    {
        get
        {
            return this._client.TryGetMultiWheelKeepActive(out var value) && value;
        }
    }

    public void Disable()
    {
        _ = this._client.DisableMultiWheelKeepActiveAsync();
    }

    public void Dispose()
    {
        this._client.Dispose();
    }
}
```

The important rule is that unavailable helper state is always treated as
`false`:

```csharp
var keepActive = sharedState.TryGetMultiWheelKeepActive(out var value) && value;
```

## Replacing Local MultiWheelFnState

Existing plugins currently have a local class similar to:

```csharp
public Boolean IsEnabled { get; private set; }
public void Toggle() { ... }
public void Disable() { ... }
```

For client plugins, do not keep a second toggle action. The physical Loupedeck
button should be assigned only to the helper plugin's `MultiWheel Fn` action.

In ATEM/JON, keep the local `MultiWheelFnState` name if that avoids touching too
many call sites, but make it an adapter:

```csharp
namespace Loupedeck.YourPlugin.MultiWheel
{
    using System;
    using Loupedeck.SharedState;

    internal sealed class MultiWheelFnState : IDisposable
    {
        private readonly LoupedeckSharedStateClient _client = new();

        public event Action Changed;

        public Boolean IsEnabled => this._client.TryGetMultiWheelKeepActive(out var value) && value;

        public MultiWheelFnState()
        {
            this._client.MultiWheelKeepActiveChanged += _ => this.Changed?.Invoke();
            _ = this._client.StartWatchingAsync();
        }

        public void Disable()
        {
            _ = this._client.DisableMultiWheelKeepActiveAsync();
        }

        public void Dispose()
        {
            this._client.Dispose();
        }
    }
}
```

Then remove or stop registering the old plugin-local `MultiWheelFnToggleCommand`
so users only bind the helper plugin action once.

## MultiWheel Decision Point

Where a MultiWheel action currently decides whether to jump back to clock or
default display, use:

```csharp
if (!multiWheelFnState.IsEnabled)
{
    multiWheelFnState.Disable();
    multiWheelDispatch.InformInActive(this);
}
```

or directly:

```csharp
if (!(sharedState.TryGetMultiWheelKeepActive(out var keepActive) && keepActive))
{
    multiWheelDispatch.InformInActive(this);
}
```

## Optional UI Updates

If a client plugin still has UI that needs to reflect the shared state, subscribe
once during service/action initialization:

```csharp
this._sharedState.MultiWheelKeepActiveChanged += value => this.ActionImageChanged();
await this._sharedState.StartWatchingAsync();
```

Do not block the Loupedeck UI thread waiting for the helper. The client library
uses short timeouts, but button rendering should still treat shared state as
best-effort.

## Manual Check

With the helper plugin running:

```sh
dotnet run --project ../LoupedeckHelperPlugin/tools/LoupedeckSharedStateTestClient -- get
dotnet run --project ../LoupedeckHelperPlugin/tools/LoupedeckSharedStateTestClient -- toggle
dotnet run --project ../LoupedeckHelperPlugin/tools/LoupedeckSharedStateTestClient -- watch
```

With the helper stopped, `get` should print `False` and exit quickly.
