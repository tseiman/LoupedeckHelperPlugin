using Loupedeck.SharedState;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "get";
var client = new LoupedeckSharedStateClient();

switch (command)
{
    case "get":
        return PrintState(client);
    case "set":
        if (args.Length < 2 || !Boolean.TryParse(args[1], out var value))
        {
            Console.Error.WriteLine("Usage: set true|false");
            return 2;
        }

        await client.SetMultiWheelKeepActiveAsync(value);
        return PrintState(client);
    case "toggle":
        await client.ToggleMultiWheelKeepActiveAsync();
        return PrintState(client);
    case "disable":
        await client.DisableMultiWheelKeepActiveAsync();
        return PrintState(client);
    default:
        Console.Error.WriteLine("Usage: get | set true|false | toggle | disable");
        return 2;
}

static Int32 PrintState(LoupedeckSharedStateClient client)
{
    if (!client.TryGetMultiWheelKeepActive(out var value))
    {
        Console.WriteLine("MultiWheel Fn: OFF (provider unavailable)");
        return 1;
    }

    Console.WriteLine(value ? "MultiWheel Fn: ON" : "MultiWheel Fn: OFF");
    return 0;
}
