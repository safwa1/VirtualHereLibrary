namespace vrhero.VirtualHere;

public readonly record struct DeviceAppeared(DeviceIdentity Identity, AvailableDevice Device);

public readonly record struct DeviceDisappeared(DeviceIdentity Identity);

public readonly record struct DeviceBecameUsed(DeviceIdentity Identity, UsedDevice Device);

public readonly record struct DeviceBecameAvailable(DeviceIdentity Identity, AvailableDevice Device);

public readonly record struct UsedDeviceUpdated(DeviceIdentity Identity, UsedDevice Device);

public readonly record struct AvailableDeviceUpdated(DeviceIdentity Identity, AvailableDevice Device);
