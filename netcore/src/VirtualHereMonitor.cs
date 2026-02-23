namespace vrhero.VirtualHere;

public sealed class VirtualHereMonitor
{
    private readonly VirtualHereIpcClient _ipcClient;
    private readonly VirtualHereResponseParser _parser;
    private readonly TimeSpan _interval;

    private Dictionary<DeviceIdentity, UsedDevice> _previousUsed = new();
    private Dictionary<DeviceIdentity, AvailableDevice> _previousAvailable = new();

    public event Action<DeviceAppeared>? DeviceAppeared;
    public event Action<DeviceDisappeared>? DeviceDisappeared;
    public event Action<DeviceBecameUsed>? DeviceBecameUsed;
    public event Action<DeviceBecameAvailable>? DeviceBecameAvailable;
    public event Action<UsedDeviceUpdated>? UsedDeviceUpdated;
    public event Action<AvailableDeviceUpdated>? AvailableDeviceUpdated;

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
        var snapshot = _parser.ParseAll(listOutput);

        var usedNow = new Dictionary<DeviceIdentity, UsedDevice>(snapshot.UsedDevices.Count);
        var availNow = new Dictionary<DeviceIdentity, AvailableDevice>(snapshot.AvailableDevices.Count);

        for (var i = 0; i < snapshot.UsedDevices.Count; i++)
        {
            var device = snapshot.UsedDevices[i];
            usedNow[ToIdentity(device.ServerName, device.DeviceId)] = device;
        }

        for (var i = 0; i < snapshot.AvailableDevices.Count; i++)
        {
            var device = snapshot.AvailableDevices[i];
            availNow[ToIdentity(device.ServerName, device.DeviceId)] = device;
        }

        EmitChanges(_previousUsed, _previousAvailable, usedNow, availNow);

        _previousUsed = usedNow;
        _previousAvailable = availNow;
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

            if (previousUsed.ContainsKey(identity))
            {
                RaiseDeviceBecameAvailable(new DeviceBecameAvailable(identity, device));
                continue;
            }

            if (!previousAvailable.TryGetValue(identity, out var oldAvailable))
            {
                RaiseDeviceAppeared(new DeviceAppeared(identity, device));
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

            if (previousAvailable.ContainsKey(identity))
            {
                RaiseDeviceBecameUsed(new DeviceBecameUsed(identity, device));
                continue;
            }

            if (!previousUsed.TryGetValue(identity, out var oldUsed))
            {
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
        }

        foreach (var pair in previousUsed)
        {
            if (availableNow.ContainsKey(pair.Key) || usedNow.ContainsKey(pair.Key))
            {
                continue;
            }

            RaiseDeviceDisappeared(new DeviceDisappeared(pair.Key));
        }
    }

    private static bool AreEquivalent(UsedDevice left, UsedDevice right)
    {
        return string.Equals(left.DeviceName, right.DeviceName, StringComparison.Ordinal)
               && string.Equals(left.DeviceId, right.DeviceId, StringComparison.Ordinal)
               && string.Equals(left.IpAddress, right.IpAddress, StringComparison.Ordinal)
               && string.Equals(left.UsedBy, right.UsedBy, StringComparison.Ordinal)
               && Nullable.Equals(left.StartTime, right.StartTime)
               && string.Equals(left.UsageTime, right.UsageTime, StringComparison.Ordinal)
               && string.Equals(left.TimeRemaining, right.TimeRemaining, StringComparison.Ordinal)
               && string.Equals(left.ServerName, right.ServerName, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(AvailableDevice left, AvailableDevice right)
    {
        return string.Equals(left.DeviceName, right.DeviceName, StringComparison.Ordinal)
               && string.Equals(left.DeviceId, right.DeviceId, StringComparison.Ordinal)
               && string.Equals(left.Status, right.Status, StringComparison.Ordinal)
               && string.Equals(left.UsageTime, right.UsageTime, StringComparison.Ordinal)
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
}
