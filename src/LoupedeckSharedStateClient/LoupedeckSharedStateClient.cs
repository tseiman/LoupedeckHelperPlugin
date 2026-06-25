namespace Loupedeck.SharedState
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public sealed class LoupedeckSharedStateClient
    {
        private const String Key = "loupedeck.shared.multiwheel.keep-active";
        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

        public Boolean TryGetMultiWheelKeepActive(out Boolean value)
        {
            try
            {
                value = this.SendAsync("get").GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                value = false;
                return false;
            }
        }

        public async Task<Boolean> GetMultiWheelKeepActiveAsync()
        {
            try
            {
                return await this.SendAsync("get").ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        public async IAsyncEnumerable<Boolean> WatchMultiWheelKeepActiveAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var endpoint = ReadEndpoint();
            await using var connection = await Connection.OpenAsync(endpoint, cancellationToken).ConfigureAwait(false);

            await connection.WriteLineAsync(CreateRequest("watch"), cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await connection.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                var response = JsonSerializer.Deserialize<Response>(line, JsonOptions());
                if (response?.Value.HasValue == true)
                {
                    yield return response.Value.Value;
                }
            }
        }

        private async Task<Boolean> SendAsync(String command)
        {
            using var timeout = new CancellationTokenSource(Timeout);
            var endpoint = ReadEndpoint();
            await using var connection = await Connection.OpenAsync(endpoint, timeout.Token).ConfigureAwait(false);

            await connection.WriteLineAsync(CreateRequest(command), timeout.Token).ConfigureAwait(false);
            var line = await connection.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<Response>(line, JsonOptions());
            if (response?.Ok != true)
            {
                throw new IOException(response?.Error ?? "Shared state request failed");
            }

            return response.Value.GetValueOrDefault(false);
        }

        private static String CreateRequest(String command) => JsonSerializer.Serialize(new Request
        {
            Command = command,
            Key = Key
        }, JsonOptions());

        private static String ReadEndpoint()
        {
            var path = DiscoveryFilePath();
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.GetProperty("endpoint").GetString();
        }

        private static String DiscoveryFilePath() => Path.Combine(DiscoveryDirectory(), "shared-state.json");

        private static String DiscoveryDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LoupedeckSharedState");
            }

            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "LoupedeckSharedState");
            }

            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return !String.IsNullOrWhiteSpace(xdgDataHome)
                ? Path.Combine(xdgDataHome, "LoupedeckSharedState")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "LoupedeckSharedState");
        }

        private static JsonSerializerOptions JsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private sealed class Request
        {
            [JsonPropertyName("cmd")]
            public String Command { get; set; }

            public String Key { get; set; }
        }

        private sealed class Response
        {
            public String Event { get; set; }

            public Boolean Ok { get; set; }

            public String Key { get; set; }

            public Boolean? Value { get; set; }

            public String Error { get; set; }
        }

        private sealed class Connection : IAsyncDisposable
        {
            private readonly Stream _stream;
            private readonly StreamReader _reader;
            private readonly StreamWriter _writer;

            private Connection(Stream stream)
            {
                this._stream = stream;
                this._reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                this._writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            }

            public static async Task<Connection> OpenAsync(String endpoint, CancellationToken cancellationToken)
            {
                if (endpoint?.StartsWith("unix:", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint.Substring("unix:".Length)), cancellationToken).ConfigureAwait(false);
                    return new Connection(new NetworkStream(socket, ownsSocket: true));
                }

                if (endpoint?.StartsWith("pipe:", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var pipe = new NamedPipeClientStream(".", endpoint.Substring("pipe:".Length), PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync((Int32)Timeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);
                    return new Connection(pipe);
                }

                throw new NotSupportedException($"Unsupported shared state endpoint: {endpoint}");
            }

            public Task WriteLineAsync(String line, CancellationToken cancellationToken) =>
                this._writer.WriteLineAsync(line.AsMemory(), cancellationToken);

            public async Task<String> ReadLineAsync(CancellationToken cancellationToken)
            {
                var line = await this._reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                return line ?? throw new EndOfStreamException();
            }

            public async ValueTask DisposeAsync()
            {
                await this._writer.DisposeAsync().ConfigureAwait(false);
                this._reader.Dispose();
                await this._stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
