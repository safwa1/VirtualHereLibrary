namespace vrhero.VirtualHere;

public readonly record struct DeviceAppeared(DeviceIdentity Identity, AvailableDevice Device);

public readonly record struct DeviceDisappeared(DeviceIdentity Identity);

public readonly record struct DeviceBecameUsed(DeviceIdentity Identity, UsedDevice Device);

public readonly record struct DeviceBecameAvailable(DeviceIdentity Identity, AvailableDevice Device);

public readonly record struct UsedDeviceUpdated(DeviceIdentity Identity, UsedDevice Device);

public readonly record struct AvailableDeviceUpdated(DeviceIdentity Identity, AvailableDevice Device);

public sealed class DeviceBoundEvent : EventArgs
{
    public DateTime Timestamp { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public int ConnectionId { get; init; }
}

public sealed class DeviceUnboundEvent : EventArgs
{
    public DateTime Timestamp { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public int ConnectionId { get; init; }
}

public sealed class DeviceUnmanagedEvent : EventArgs
{
    public DateTime Timestamp { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
}

public sealed class DeviceFoundEvent : EventArgs
{
    public DateTime Timestamp { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string Speed { get; init; } = string.Empty;
    public int Address { get; init; }
}

public sealed class ClientConnectedEvent : EventArgs
{
    public DateTime Timestamp { get; init; }
    public string ClientIp { get; init; } = string.Empty;
    public int ConnectionId { get; init; }
    public string ConnectionType { get; init; } = "VirtualHere";
}

public sealed class ClientDisconnectedEvent : EventArgs
{
    public DateTime Timestamp { get; init; }
    public int ConnectionId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
