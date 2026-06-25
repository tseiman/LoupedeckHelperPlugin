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
    using System.Threading.Tasks;

    public sealed class LoupedeckSharedStateClient : IDisposable
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(250);
        private CancellationTokenSource _watchCancellation;
        private Task _watchTask;
        private Boolean _loggedUnavailable;

        public event Action<Boolean> MultiWheelKeepActiveChanged;

        public Boolean IsAvailable
        {
            get
            {
                try
                {
                    _ = this.SendBooleanCommandAsync("get", null, CancellationToken.None).GetAwaiter().GetResult();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Boolean TryGetMultiWheelKeepActive(out Boolean value)
        {
            try
            {
                value = this.SendBooleanCommandAsync("get", null, CancellationToken.None).GetAwaiter().GetResult();
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
                return await this.SendBooleanCommandAsync("get", null, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        public async Task SetMultiWheelKeepActiveAsync(Boolean value) =>
            await this.SendCommandNoThrowAsync("set", value).ConfigureAwait(false);

        public async Task ToggleMultiWheelKeepActiveAsync() =>
            await this.SendCommandNoThrowAsync("toggle", null).ConfigureAwait(false);

        public async Task DisableMultiWheelKeepActiveAsync() =>
            await this.SendCommandNoThrowAsync("disable", null).ConfigureAwait(false);

        public Task StartWatchingAsync()
        {
            if (this._watchTask != null && !this._watchTask.IsCompleted)
            {
                return Task.CompletedTask;
            }

            this._watchCancellation = new CancellationTokenSource();
            this._watchTask = Task.Run(() => this.WatchLoopAsync(this._watchCancellation.Token));
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this._watchCancellation?.Cancel();
            this._watchCancellation?.Dispose();
        }

        private async Task<Boolean> SendBooleanCommandAsync(String command, Boolean? value, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DefaultTimeout);

            var endpoint = this.ResolveEndpoint();
            await using var connection = await IpcConnection.ConnectAsync(endpoint, timeout.Token).ConfigureAwait(false);
            var request = new StateRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Command = command,
                Key = SharedStateConstants.MultiWheelKeepActiveKey,
                Value = value
            };

            await connection.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions()), timeout.Token).ConfigureAwait(false);
            var responseLine = await connection.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<StateResponse>(responseLine, JsonOptions());
            if (response?.Ok != true)
            {
                throw new IOException(response?.Error ?? "Shared state request failed");
            }

            return response.Value.GetValueOrDefault(false);
        }

        private async Task SendCommandNoThrowAsync(String command, Boolean? value)
        {
            try
            {
                _ = await this.SendBooleanCommandAsync(command, value, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                this.LogUnavailableOnce();
            }
        }

        private async Task WatchLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var endpoint = this.ResolveEndpoint();
                    await using var connection = await IpcConnection.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    await connection.WriteLineAsync(JsonSerializer.Serialize(new StateRequest
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Command = "subscribe",
                        Key = SharedStateConstants.MultiWheelKeepActiveKey
                    }, JsonOptions()), cancellationToken).ConfigureAwait(false);

                    this._loggedUnavailable = false;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await connection.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        var evt = JsonSerializer.Deserialize<StateEvent>(line, JsonOptions());
                        if (String.Equals(evt?.Event, "changed", StringComparison.Ordinal)
                            && String.Equals(evt.Key, SharedStateConstants.MultiWheelKeepActiveKey, StringComparison.Ordinal))
                        {
                            this.MultiWheelKeepActiveChanged?.Invoke(evt.Value.GetValueOrDefault(false));
                        }
                    }
                }
                catch when (!cancellationToken.IsCancellationRequested)
                {
                    this.LogUnavailableOnce();
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private SharedStateEndpoint ResolveEndpoint()
        {
            if (SharedStateDiscovery.TryRead(out var endpoint))
            {
                return endpoint;
            }

            this.LogUnavailableOnce();
            throw new FileNotFoundException("Shared state discovery file not found", SharedStateDiscovery.GetDiscoveryFilePath());
        }

        private void LogUnavailableOnce()
        {
            if (this._loggedUnavailable)
            {
                return;
            }

            this._loggedUnavailable = true;
            Console.Error.WriteLine("[LoupedeckSharedStateClient] Shared state provider unavailable, using default false");
        }

        private static JsonSerializerOptions JsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private sealed class StateRequest
        {
            public String Id { get; set; }

            [JsonPropertyName("cmd")]
            public String Command { get; set; }

            public String Key { get; set; }
            public Boolean? Value { get; set; }
        }

        private sealed class StateResponse
        {
            public String Id { get; set; }
            public Boolean Ok { get; set; }
            public Boolean? Value { get; set; }
            public String Error { get; set; }
        }

        private sealed class StateEvent
        {
            public String Event { get; set; }
            public String Key { get; set; }
            public Boolean? Value { get; set; }
        }

        private sealed class IpcConnection : IAsyncDisposable
        {
            private readonly Stream _stream;
            private readonly StreamReader _reader;
            private readonly StreamWriter _writer;

            private IpcConnection(Stream stream)
            {
                this._stream = stream;
                this._reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                this._writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            }

            public static async Task<IpcConnection> ConnectAsync(SharedStateEndpoint endpoint, CancellationToken cancellationToken)
            {
                if (endpoint.IsUnix)
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint.Address), cancellationToken).ConfigureAwait(false);
                    return new IpcConnection(new NetworkStream(socket, ownsSocket: true));
                }

                if (endpoint.IsNamedPipe)
                {
                    var pipe = new NamedPipeClientStream(".", endpoint.Address, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync((Int32)DefaultTimeout.TotalMilliseconds, cancellationToken).ConfigureAwait(false);
                    return new IpcConnection(pipe);
                }

                throw new NotSupportedException($"Unsupported shared state endpoint: {endpoint.RawValue}");
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
