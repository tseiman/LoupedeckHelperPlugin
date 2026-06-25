using Loupedeck.SharedState;

using var client = new LoupedeckSharedStateClient();
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "get";

try
{
    switch (command)
    {
        case "get":
            Console.WriteLine(await client.GetMultiWheelKeepActiveAsync());
            break;
        case "set":
            if (args.Length < 2 || !Boolean.TryParse(args[1], out var value))
            {
                Console.Error.WriteLine("Usage: set true|false");
                return 2;
            }

            await client.SetMultiWheelKeepActiveAsync(value);
            Console.WriteLine(value);
            break;
        case "toggle":
            await client.ToggleMultiWheelKeepActiveAsync();
            Console.WriteLine(await client.GetMultiWheelKeepActiveAsync());
            break;
        case "disable":
            await client.DisableMultiWheelKeepActiveAsync();
            Console.WriteLine(false);
            break;
        case "watch":
            client.MultiWheelKeepActiveChanged += value => Console.WriteLine(value);
            await client.StartWatchingAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan);
            break;
        default:
            Console.Error.WriteLine("Usage: get | set true|false | toggle | disable | watch");
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

return 0;
