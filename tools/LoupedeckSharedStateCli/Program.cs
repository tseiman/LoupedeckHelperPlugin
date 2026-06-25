using Loupedeck.SharedState;

var client = new LoupedeckSharedStateClient();

if (!PrintState(client))
{
    return 1;
}

await foreach (var value in client.WatchMultiWheelKeepActiveAsync())
{
    PrintValue(value);
}

return 0;

static Boolean PrintState(LoupedeckSharedStateClient client)
{
    if (!client.TryGetMultiWheelKeepActive(out var value))
    {
        Console.WriteLine("MultiWheel Fn: OFF (provider unavailable)");
        return false;
    }

    PrintValue(value);
    return true;
}

static void PrintValue(Boolean value)
{
    Console.WriteLine(value ? "MultiWheel Fn: ON" : "MultiWheel Fn: OFF");
}
