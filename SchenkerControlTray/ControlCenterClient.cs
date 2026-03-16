using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SchenkerControlTray;

internal sealed class ControlCenterClient
{
    private const string Host = "127.0.0.1";
    private const int Port = 13688;
    private const string ClientId = "OcTool";
    private const string Username = "OcToolUser";
    private const string Password = "OcToolPwd123";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task<StatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
        => ExecuteWithRecoveryAsync(async token =>
        {
            EnsureServiceRunning();
            return await QueryStatusWithRetryAsync(TimeSpan.FromSeconds(8), token);
        }, cancellationToken);

    public Task<StatusSnapshot> SetProfileAsync(ProfileMode mode, int? profileIndex = null, CancellationToken cancellationToken = default)
        => ExecuteWithRecoveryAsync(async token =>
        {
            EnsureServiceRunning();
            await SendProfileCommandAsync(mode, profileIndex, token);
            await Task.Delay(400, token);

            return await QueryStatusUntilAsync(
                snapshot => snapshot.FanStatus is not null &&
                            snapshot.FanStatus.CurrentMode == mode &&
                            (!profileIndex.HasValue || snapshot.FanStatus.CurrentProfileIndex == profileIndex.Value),
                TimeSpan.FromSeconds(10),
                token);
        }, cancellationToken);

    public async Task ReapplyCurrentProfileAsync(FanStatus status, CancellationToken cancellationToken = default)
    {
        await SetProfileAsync(status.CurrentMode, status.CurrentProfileIndex, cancellationToken);
    }

    public void LaunchControlCenter()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = AppPaths.ControlCenterAppId,
            UseShellExecute = true,
        });
    }

    private static async Task SendProfileCommandAsync(ProfileMode mode, int? profileIndex, CancellationToken cancellationToken)
    {
        using var mqtt = new RawMqttClient(Host, Port);
        await mqtt.ConnectAsync(ClientId, Username, Password, cancellationToken);

        object payload = profileIndex is int index
            ? new { Action = mode.ActionName(), ProfileIndex = index }
            : new { Action = mode.ActionName() };

        await mqtt.PublishJsonAsync("Fan/Control", payload, cancellationToken);
    }

    private static async Task<StatusSnapshot> QueryStatusWithRetryAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var snapshot = await QueryStatusOnceAsync(TimeSpan.FromSeconds(3), cancellationToken);
        if (snapshot.FanStatus is not null)
        {
            return snapshot;
        }

        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(500, cancellationToken);
            snapshot = await QueryStatusOnceAsync(TimeSpan.FromSeconds(3), cancellationToken);
            if (snapshot.FanStatus is not null)
            {
                return snapshot;
            }
        }

        throw new TimeoutException("No status reply received from Control Center.");
    }

    private static async Task<StatusSnapshot> QueryStatusUntilAsync(Func<StatusSnapshot, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        StatusSnapshot? lastSnapshot = null;
        var end = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastSnapshot = await QueryStatusOnceAsync(TimeSpan.FromSeconds(3), cancellationToken);
            if (lastSnapshot.FanStatus is not null && predicate(lastSnapshot))
            {
                return lastSnapshot;
            }

            await Task.Delay(500, cancellationToken);
        }

        if (lastSnapshot?.FanStatus is not null)
        {
            return lastSnapshot;
        }

        throw new TimeoutException("Profile command was sent, but no status reply came back from Control Center.");
    }

    private static async Task<StatusSnapshot> QueryStatusOnceAsync(TimeSpan receiveTimeout, CancellationToken cancellationToken)
    {
        using var mqtt = new RawMqttClient(Host, Port);
        await mqtt.ConnectAsync(ClientId, Username, Password, cancellationToken);
        await mqtt.SubscribeAsync(["Fan/Status", "Tray/Status", "Customize/SupportInfo"], cancellationToken);
        await mqtt.PublishJsonAsync("Fan/Control", new { Action = "GETSTATUS" }, cancellationToken);
        return await CollectStatusAsync(mqtt, receiveTimeout, cancellationToken);
    }

    private static async Task<T> ExecuteWithRecoveryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            return await operation(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsRecoverable(ex))
        {
            RestartService();
            return await operation(cancellationToken);
        }
    }

    private static bool IsRecoverable(Exception ex)
        => ex is TimeoutException or EndOfStreamException or IOException or SocketException;

    private static async Task<StatusSnapshot> CollectStatusAsync(RawMqttClient mqtt, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var end = DateTime.UtcNow + timeout;
        FanStatus? fanStatus = null;
        TrayStatus? trayStatus = null;
        SupportInfo? supportInfo = null;

        while (DateTime.UtcNow < end)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = end - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var message = await mqtt.TryReceivePublishAsync(remaining, cancellationToken);
            if (message is null)
            {
                continue;
            }

            switch (message.Topic)
            {
                case "Fan/Status":
                    fanStatus = Deserialize<FanStatus>(message.PayloadJson);
                    break;
                case "Tray/Status":
                    trayStatus = Deserialize<TrayStatus>(message.PayloadJson);
                    break;
                case "Customize/SupportInfo":
                    supportInfo = Deserialize<SupportInfo>(message.PayloadJson);
                    break;
            }

            if (fanStatus is not null && trayStatus is not null)
            {
                break;
            }
        }

        return new StatusSnapshot
        {
            FanStatus = fanStatus,
            TrayStatus = trayStatus,
            SupportInfo = supportInfo,
        };
    }

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static void EnsureServiceRunning()
    {
        if (IsBrokerListening())
        {
            return;
        }

        var existing = Process.GetProcessesByName("GCUService");
        if (existing.Length > 0)
        {
            WaitForBroker(TimeSpan.FromSeconds(2));
            if (IsBrokerListening())
            {
                return;
            }

            foreach (var process in existing)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
                catch
                {
                    // Best effort.
                }
            }
        }

        StartServiceProcess();

        if (!WaitForBroker(TimeSpan.FromSeconds(8)))
        {
            throw new TimeoutException($"GCUService did not open the MQTT broker on {Host}:{Port}.");
        }
    }

    private static void RestartService()
    {
        foreach (var process in Process.GetProcessesByName("GCUService"))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
            catch
            {
                // Best effort.
            }
        }

        StartServiceProcess();
        if (!WaitForBroker(TimeSpan.FromSeconds(8)))
        {
            throw new TimeoutException($"GCUService did not open the MQTT broker on {Host}:{Port} after restart.");
        }
    }

    private static void StartServiceProcess()
    {
        if (!File.Exists(AppPaths.GcuServicePath))
        {
            throw new FileNotFoundException("GCUService.exe not found.", AppPaths.GcuServicePath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.GcuServicePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(AppPaths.GcuServicePath)!,
        });
    }

    private static bool WaitForBroker(TimeSpan timeout)
    {
        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            if (IsBrokerListening())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return IsBrokerListening();
    }

    private static bool IsBrokerListening()
    {
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(Host, Port);
            return connectTask.Wait(TimeSpan.FromMilliseconds(500)) && tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }

    private sealed class RawMqttClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;

        public RawMqttClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAsync(string clientId, string username, string password, CancellationToken cancellationToken)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port, cancellationToken);
            _stream = _tcpClient.GetStream();

            var variableHeader = EncodeString("MQTT")
                .Concat(new byte[] { 4, 0xC2, 0, 60 })
                .ToArray();
            var payload = EncodeString(clientId)
                .Concat(EncodeString(username))
                .Concat(EncodeString(password))
                .ToArray();

            var packet = new List<byte> { 0x10 };
            packet.AddRange(EncodeRemainingLength(variableHeader.Length + payload.Length));
            packet.AddRange(variableHeader);
            packet.AddRange(payload);

            await _stream.WriteAsync(packet.ToArray(), cancellationToken);

            var (firstByte, body) = await ReceivePacketAsync(TimeSpan.FromSeconds(5), cancellationToken);
            if (firstByte != 0x20 || body.Length < 2 || body[1] != 0)
            {
                throw new InvalidOperationException($"MQTT connect failed: 0x{firstByte:X2} {Convert.ToHexString(body)}");
            }
        }

        public async Task SubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken)
        {
            EnsureConnected();

            var payload = new List<byte>();
            payload.AddRange(new byte[] { 0, 1 });
            foreach (var topic in topics)
            {
                payload.AddRange(EncodeString(topic));
                payload.Add(0);
            }

            var packet = new List<byte> { 0x82 };
            packet.AddRange(EncodeRemainingLength(payload.Count));
            packet.AddRange(payload);

            await _stream!.WriteAsync(packet.ToArray(), cancellationToken);
            await ReceivePacketAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }

        public async Task PublishJsonAsync(string topic, object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            await PublishAsync(topic, json, cancellationToken);
        }

        public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
        {
            EnsureConnected();

            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var topicBytes = EncodeString(topic);
            var packet = new List<byte> { 0x30 };
            packet.AddRange(EncodeRemainingLength(topicBytes.Length + payloadBytes.Length));
            packet.AddRange(topicBytes);
            packet.AddRange(payloadBytes);

            await _stream!.WriteAsync(packet.ToArray(), cancellationToken);
        }

        public async Task<PublishMessage?> TryReceivePublishAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                var (firstByte, body) = await ReceivePacketAsync(timeout, cancellationToken);
                if ((firstByte >> 4) != 3 || body.Length < 2)
                {
                    return null;
                }

                var topicLength = (body[0] << 8) | body[1];
                if (body.Length < topicLength + 2)
                {
                    return null;
                }

                var topic = Encoding.UTF8.GetString(body, 2, topicLength);
                var payload = Encoding.UTF8.GetString(body, 2 + topicLength, body.Length - topicLength - 2);
                return new PublishMessage(topic, payload);
            }
            catch (TimeoutException)
            {
                return null;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                return null;
            }
        }

        private async Task<(byte FirstByte, byte[] Body)> ReceivePacketAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            EnsureConnected();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            var token = cts.Token;

            try
            {
                var first = await ReadExactAsync(1, token);
                if (first.Length == 0)
                {
                    throw new EndOfStreamException("MQTT stream closed.");
                }

                var multiplier = 1;
                var remainingLength = 0;
                while (true)
                {
                    var next = await ReadExactAsync(1, token);
                    var encoded = next[0];
                    remainingLength += (encoded & 127) * multiplier;
                    if ((encoded & 128) == 0)
                    {
                        break;
                    }
                    multiplier *= 128;
                }

                var body = remainingLength == 0
                    ? Array.Empty<byte>()
                    : await ReadExactAsync(remainingLength, token);

                return (first[0], body);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out waiting for MQTT data from {Host}:{Port}.");
            }
        }

        private async Task<byte[]> ReadExactAsync(int count, CancellationToken cancellationToken)
        {
            var buffer = new byte[count];
            var offset = 0;

            while (offset < count)
            {
                var read = await _stream!.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);
                if (read == 0)
                {
                    throw new EndOfStreamException("Unexpected end of MQTT stream.");
                }
                offset += read;
            }

            return buffer;
        }

        private void EnsureConnected()
        {
            if (_stream is null)
            {
                throw new InvalidOperationException("MQTT client is not connected.");
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }
    }

    private sealed record PublishMessage(string Topic, string PayloadJson);

    private static byte[] EncodeString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return
        [
            (byte)(bytes.Length >> 8),
            (byte)(bytes.Length & 0xFF),
            .. bytes,
        ];
    }

    private static IEnumerable<byte> EncodeRemainingLength(int value)
    {
        do
        {
            var encoded = value % 128;
            value /= 128;
            if (value > 0)
            {
                yield return (byte)(encoded | 0x80);
            }
            else
            {
                yield return (byte)encoded;
            }
        }
        while (value > 0);
    }
}
