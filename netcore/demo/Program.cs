using vrhero.VirtualHere;

var usageManager = new UsageTimeManager();
var parser = new VirtualHereResponseParser(usageManager);
var ipcClient = new VirtualHereIpcClient(connectTimeout: TimeSpan.FromSeconds(2));
var monitor = new VirtualHereMonitor(ipcClient, parser, TimeSpan.FromMilliseconds(350));

monitor.ServerStartListening += (_, _) => Console.WriteLine("[Server] Listening via vhclient IPC");
monitor.Reading += (_, text) => Console.WriteLine($"[Reading] {text.Length} chars");
monitor.DeviceBound += (_, e) =>
    Console.WriteLine($"[DeviceBound] time={e.Timestamp:O} device={e.DeviceName} id={e.DeviceId} vendor={e.VendorId} product={e.ProductId} connection={e.ConnectionId}");
monitor.DeviceUnbound += (_, e) =>
    Console.WriteLine($"[DeviceUnbound] time={e.Timestamp:O} device={e.DeviceName} id={e.DeviceId} vendor={e.VendorId} product={e.ProductId} connection={e.ConnectionId}");
monitor.DeviceUnmanaged += (_, e) =>
    Console.WriteLine($"[DeviceUnmanaged] time={e.Timestamp:O} device={e.DeviceName} id={e.DeviceId} vendor={e.VendorId} product={e.ProductId}");
monitor.DeviceFound += (_, e) =>
    Console.WriteLine($"[DeviceFound] time={e.Timestamp:O} device={e.DeviceName} id={e.DeviceId} vendor={e.VendorId} product={e.ProductId} address={e.Address}");
monitor.ClientConnected += (_, e) =>
    Console.WriteLine($"[ClientConnected] time={e.Timestamp:O} ip={e.ClientIp} connection={e.ConnectionId} type={e.ConnectionType}");
monitor.ClientDisconnected += (_, e) =>
    Console.WriteLine($"[ClientDisconnected] time={e.Timestamp:O} connection={e.ConnectionId} reason={e.Reason}");

monitor.DeviceAppeared += e =>
    Console.WriteLine($"[Appeared] server={e.Identity.ServerName} id={e.Identity.DeviceId} name={e.Device.DeviceName} status={e.Device.Status} usage={e.Device.UsageTimeAsString()}");
monitor.DeviceDisappeared += e =>
    Console.WriteLine($"[Disappeared] server={e.Identity.ServerName} id={e.Identity.DeviceId}");
monitor.DeviceBecameUsed += e =>
    Console.WriteLine($"[BecameUsed] server={e.Identity.ServerName} id={e.Identity.DeviceId} name={e.Device.DeviceName} usedBy={e.Device.UsedBy} ip={e.Device.IpAddress} since={e.Device.StartTime:O} usage={e.Device.UsageTime:g} remaining={e.Device.TimeRemaining}");
monitor.DeviceBecameAvailable += e =>
    Console.WriteLine($"[BecameAvailable] server={e.Identity.ServerName} id={e.Identity.DeviceId} name={e.Device.DeviceName} status={e.Device.Status} usage={e.Device.UsageTimeAsString()}");
monitor.UsedDeviceUpdated += e =>
    Console.WriteLine($"[UsedUpdated] server={e.Identity.ServerName} id={e.Identity.DeviceId} usedBy={e.Device.UsedBy} usage={e.Device.UsageTime:g} remaining={e.Device.TimeRemaining}");
monitor.AvailableDeviceUpdated += e =>
    Console.WriteLine($"[AvailableUpdated] server={e.Identity.ServerName} id={e.Identity.DeviceId} status={e.Device.Status} usage={e.Device.UsageTimeAsString()}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("VirtualHere monitor started. Press Ctrl+C to stop.");

try
{
    await monitor.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("VirtualHere monitor stopped.");
}
