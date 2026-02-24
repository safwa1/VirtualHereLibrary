using System.Text.RegularExpressions;

namespace vrhero.VirtualHere;

public sealed class VirtualHereMonitor
{
    private static readonly Regex VendorProductRegex = new(@"(?<vendor>[0-9a-fA-F]{4}):(?<product>[0-9a-fA-F]{4})", RegexOptions.Compiled);
    private readonly VirtualHereIpcClient _ipcClient;
    private readonly VirtualHereResponseParser _parser;
    private readonly TimeSpan _interval;

    private Dictionary<DeviceIdentity, UsedDevice> _previousUsed = new();
    private Dictionary<DeviceIdentity, AvailableDevice> _previousAvailable = new();
    private HashSet<string> _previousClients = new(StringComparer.OrdinalIgnoreCase);
    private bool _isFirstSuccessfulPoll;

    public event Action<DeviceAppeared>? DeviceAppeared;
    public event Action<DeviceDisappeared>? DeviceDisappeared;
    public event Action<DeviceBecameUsed>? DeviceBecameUsed;
    public event Action<DeviceBecameAvailable>? DeviceBecameAvailable;
    public event Action<UsedDeviceUpdated>? UsedDeviceUpdated;
    public event Action<AvailableDeviceUpdated>? AvailableDeviceUpdated;

    public event EventHandler<DeviceBoundEvent>? DeviceBound;
    public event EventHandler<DeviceUnboundEvent>? DeviceUnbound;
    public event EventHandler<DeviceUnmanagedEvent>? DeviceUnmanaged;
    public event EventHandler<DeviceFoundEvent>? DeviceFound;
    public event EventHandler<ClientConnectedEvent>? ClientConnected;
    public event EventHandler<ClientDisconnectedEvent>? ClientDisconnected;
    public event EventHandler? ServerStartListening;
    public event EventHandler<string>? Reading;

    public VirtualHereMonitor(VirtualHereIpcClient ipcClient, VirtualHereResponseParser parser, TimeSpan? interval = null)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _interval = interval ?? TimeSpan.FromMilliseconds(350);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            await PollOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        var listOutput = await _ipcClient.SendAsync("LIST", cancellationToken).ConfigureAwait(false);
        RaiseReading(listOutput);

        if (!_isFirstSuccessfulPoll)
        {
            _isFirstSuccessfulPoll = true;
            RaiseServerStartListening();
        }

        var snapshot = _parser.ParseAll(listOutput);

        var usedNow = new Dictionary<DeviceIdentity, UsedDevice>(snapshot.UsedDevices.Count);
        var availNow = new Dictionary<DeviceIdentity, AvailableDevice>(snapshot.AvailableDevices.Count);
        var clientsNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < snapshot.UsedDevices.Count; i++)
        {
            var device = snapshot.UsedDevices[i];
            usedNow[ToIdentity(device.ServerName, device.DeviceId)] = device;

            var clientKey = NormalizeClientKey(device.UsedBy);
            if (!string.IsNullOrEmpty(clientKey))
            {
                clientsNow.Add(clientKey);
            }
        }

        for (var i = 0; i < snapshot.AvailableDevices.Count; i++)
        {
            var device = snapshot.AvailableDevices[i];
            availNow[ToIdentity(device.ServerName, device.DeviceId)] = device;
        }

        EmitChanges(_previousUsed, _previousAvailable, usedNow, availNow);
        EmitClientChanges(_previousClients, clientsNow);

        _previousUsed = usedNow;
        _previousAvailable = availNow;
        _previousClients = clientsNow;
    }

    private static DeviceIdentity ToIdentity(string? serverName, string? deviceId)
        => new(serverName ?? string.Empty, deviceId ?? string.Empty);

    private void EmitChanges(
        Dictionary<DeviceIdentity, UsedDevice> previousUsed,
        Dictionary<DeviceIdentity, AvailableDevice> previousAvailable,
        Dictionary<DeviceIdentity, UsedDevice> usedNow,
        Dictionary<DeviceIdentity, AvailableDevice> availableNow)
    {
        foreach (var pair in availableNow)
        {
            var identity = pair.Key;
            var device = pair.Value;

            if (previousUsed.TryGetValue(identity, out var oldUsedDevice))
            {
                RaiseDeviceBecameAvailable(new DeviceBecameAvailable(identity, device));
                RaiseDeviceUnbound(BuildUnboundEvent(oldUsedDevice));
                continue;
            }

            if (!previousAvailable.TryGetValue(identity, out var oldAvailable))
            {
                RaiseDeviceAppeared(new DeviceAppeared(identity, device));
                RaiseDeviceFound(BuildFoundEvent(device));
                continue;
            }

            if (!AreEquivalent(oldAvailable, device))
            {
                RaiseAvailableDeviceUpdated(new AvailableDeviceUpdated(identity, device));
            }
        }

        foreach (var pair in usedNow)
        {
            var identity = pair.Key;
            var device = pair.Value;

            if (previousAvailable.TryGetValue(identity, out var oldAvailableDevice))
            {
                RaiseDeviceBecameUsed(new DeviceBecameUsed(identity, device));
                RaiseDeviceBound(BuildBoundEvent(device, oldAvailableDevice));
                continue;
            }

            if (!previousUsed.TryGetValue(identity, out var oldUsed))
            {
                RaiseDeviceBound(BuildBoundEvent(device, null));
                continue;
            }

            if (!AreEquivalent(oldUsed, device))
            {
                RaiseUsedDeviceUpdated(new UsedDeviceUpdated(identity, device));
            }
        }

        foreach (var pair in previousAvailable)
        {
            if (availableNow.ContainsKey(pair.Key) || usedNow.ContainsKey(pair.Key))
            {
                continue;
            }

            RaiseDeviceDisappeared(new DeviceDisappeared(pair.Key));
            RaiseDeviceUnmanaged(BuildUnmanagedEvent(pair.Value.DeviceName, pair.Value.DeviceId));
        }

        foreach (var pair in previousUsed)
        {
            if (availableNow.ContainsKey(pair.Key) || usedNow.ContainsKey(pair.Key))
            {
                continue;
            }

            RaiseDeviceDisappeared(new DeviceDisappeared(pair.Key));
            RaiseDeviceUnmanaged(BuildUnmanagedEvent(pair.Value.DeviceName, pair.Value.DeviceId));
        }
    }

    private void EmitClientChanges(HashSet<string> previousClients, HashSet<string> clientsNow)
    {
        foreach (var client in clientsNow)
        {
            if (previousClients.Contains(client))
            {
                continue;
            }

            RaiseClientConnected(new ClientConnectedEvent
            {
                Timestamp = DateTime.Now,
                ClientIp = client,
                ConnectionId = ParseConnectionId(client),
                ConnectionType = "VirtualHere IPC"
            });
        }

        foreach (var client in previousClients)
        {
            if (clientsNow.Contains(client))
            {
                continue;
            }

            RaiseClientDisconnected(new ClientDisconnectedEvent
            {
                Timestamp = DateTime.Now,
                ConnectionId = ParseConnectionId(client),
                Reason = $"Client no longer present: {client}"
            });
        }
    }

    private static string NormalizeClientKey(string usedBy)
    {
        if (string.IsNullOrWhiteSpace(usedBy))
        {
            return string.Empty;
        }

        var value = usedBy.Trim();
        var separator = value.IndexOf(' ');
        return separator > 0 ? value[..separator] : value;
    }

    private static int ParseConnectionId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var numberStart = raw.LastIndexOf('#');
        if (numberStart >= 0 && numberStart + 1 < raw.Length && int.TryParse(raw[(numberStart + 1)..], out var id))
        {
            return id;
        }

        return 0;
    }

    private static DeviceBoundEvent BuildBoundEvent(UsedDevice usedDevice, AvailableDevice? oldAvailable)
    {
        ExtractVendorProduct(usedDevice.DeviceId, out var vendorId, out var productId);

        return new DeviceBoundEvent
        {
            Timestamp = DateTime.Now,
            DeviceName = usedDevice.DeviceName,
            DeviceId = usedDevice.DeviceId,
            VendorId = vendorId,
            ProductId = productId,
            ConnectionId = ParseConnectionId(usedDevice.UsedBy)
        };
    }

    private static DeviceUnboundEvent BuildUnboundEvent(UsedDevice oldUsed)
    {
        ExtractVendorProduct(oldUsed.DeviceId, out var vendorId, out var productId);

        return new DeviceUnboundEvent
        {
            Timestamp = DateTime.Now,
            DeviceName = oldUsed.DeviceName,
            DeviceId = oldUsed.DeviceId,
            VendorId = vendorId,
            ProductId = productId,
            ConnectionId = ParseConnectionId(oldUsed.UsedBy)
        };
    }

    private static DeviceFoundEvent BuildFoundEvent(AvailableDevice device)
    {
        ExtractVendorProduct(device.DeviceId, out var vendorId, out var productId);

        return new DeviceFoundEvent
        {
            Timestamp = DateTime.Now,
            DeviceName = device.DeviceName,
            DeviceId = device.DeviceId,
            VendorId = vendorId,
            ProductId = productId,
            Speed = string.Empty,
            Address = TryParseAddress(device.DeviceId)
        };
    }

    private static DeviceUnmanagedEvent BuildUnmanagedEvent(string deviceName, string deviceId)
    {
        ExtractVendorProduct(deviceId, out var vendorId, out var productId);

        return new DeviceUnmanagedEvent
        {
            Timestamp = DateTime.Now,
            DeviceName = deviceName,
            DeviceId = deviceId,
            VendorId = vendorId,
            ProductId = productId
        };
    }

    private static int TryParseAddress(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return 0;
        }

        var segments = deviceId.Split('.');
        if (segments.Length > 0 && int.TryParse(segments[^1], out var address))
        {
            return address;
        }

        return 0;
    }

    private static void ExtractVendorProduct(string raw, out string vendorId, out string productId)
    {
        var match = VendorProductRegex.Match(raw ?? string.Empty);
        if (match.Success)
        {
            vendorId = match.Groups["vendor"].Value;
            productId = match.Groups["product"].Value;
            return;
        }

        vendorId = string.Empty;
        productId = string.Empty;
    }

    private static bool AreEquivalent(UsedDevice left, UsedDevice right)
    {
        return string.Equals(left.DeviceName, right.DeviceName, StringComparison.Ordinal)
               && string.Equals(left.DeviceId, right.DeviceId, StringComparison.Ordinal)
               && string.Equals(left.IpAddress, right.IpAddress, StringComparison.Ordinal)
               && string.Equals(left.UsedBy, right.UsedBy, StringComparison.Ordinal)
               && left.StartTime == right.StartTime
               && left.UsageTime == right.UsageTime
               && string.Equals(left.TimeRemaining, right.TimeRemaining, StringComparison.Ordinal)
               && string.Equals(left.ServerName, right.ServerName, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(AvailableDevice left, AvailableDevice right)
    {
        return string.Equals(left.DeviceName, right.DeviceName, StringComparison.Ordinal)
               && string.Equals(left.DeviceId, right.DeviceId, StringComparison.Ordinal)
               && string.Equals(left.Status, right.Status, StringComparison.Ordinal)
               && left.UsageTime == right.UsageTime
               && string.Equals(left.ServerName, right.ServerName, StringComparison.Ordinal);
    }

    private void RaiseDeviceAppeared(DeviceAppeared evt)
    {
        var handler = DeviceAppeared;
        handler?.Invoke(evt);
    }

    private void RaiseDeviceDisappeared(DeviceDisappeared evt)
    {
        var handler = DeviceDisappeared;
        handler?.Invoke(evt);
    }

    private void RaiseDeviceBecameUsed(DeviceBecameUsed evt)
    {
        var handler = DeviceBecameUsed;
        handler?.Invoke(evt);
    }

    private void RaiseDeviceBecameAvailable(DeviceBecameAvailable evt)
    {
        var handler = DeviceBecameAvailable;
        handler?.Invoke(evt);
    }

    private void RaiseUsedDeviceUpdated(UsedDeviceUpdated evt)
    {
        var handler = UsedDeviceUpdated;
        handler?.Invoke(evt);
    }

    private void RaiseAvailableDeviceUpdated(AvailableDeviceUpdated evt)
    {
        var handler = AvailableDeviceUpdated;
        handler?.Invoke(evt);
    }

    private void RaiseDeviceBound(DeviceBoundEvent evt)
    {
        var handler = DeviceBound;
        handler?.Invoke(this, evt);
    }

    private void RaiseDeviceUnbound(DeviceUnboundEvent evt)
    {
        var handler = DeviceUnbound;
        handler?.Invoke(this, evt);
    }

    private void RaiseDeviceUnmanaged(DeviceUnmanagedEvent evt)
    {
        var handler = DeviceUnmanaged;
        handler?.Invoke(this, evt);
    }

    private void RaiseDeviceFound(DeviceFoundEvent evt)
    {
        var handler = DeviceFound;
        handler?.Invoke(this, evt);
    }

    private void RaiseClientConnected(ClientConnectedEvent evt)
    {
        var handler = ClientConnected;
        handler?.Invoke(this, evt);
    }

    private void RaiseClientDisconnected(ClientDisconnectedEvent evt)
    {
        var handler = ClientDisconnected;
        handler?.Invoke(this, evt);
    }

    private void RaiseServerStartListening()
    {
        var handler = ServerStartListening;
        handler?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseReading(string data)
    {
        var handler = Reading;
        handler?.Invoke(this, data);
    }
}
