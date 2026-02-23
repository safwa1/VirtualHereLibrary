using vrhero.VirtualHere;

var usageManager = new UsageTimeManager();
var parser = new VirtualHereResponseParser(usageManager);
var ipcClient = new VirtualHereIpcClient(connectTimeout: TimeSpan.FromSeconds(2));
var monitor = new VirtualHereMonitor(ipcClient, parser, TimeSpan.FromMilliseconds(350));

monitor.DeviceAppeared += e =>
    Console.WriteLine($"[Appeared] {e.Identity.ServerName}/{e.Identity.DeviceId} {e.Device.DeviceName}");
monitor.DeviceDisappeared += e =>
    Console.WriteLine($"[Disappeared] {e.Identity.ServerName}/{e.Identity.DeviceId}");
monitor.DeviceBecameUsed += e =>
    Console.WriteLine($"[BecameUsed] {e.Identity.ServerName}/{e.Identity.DeviceId} by {e.Device.UsedBy}");
monitor.DeviceBecameAvailable += e =>
    Console.WriteLine($"[BecameAvailable] {e.Identity.ServerName}/{e.Identity.DeviceId} status={e.Device.Status}");
monitor.UsedDeviceUpdated += e =>
    Console.WriteLine($"[UsedUpdated] {e.Identity.ServerName}/{e.Identity.DeviceId} usage={e.Device.UsageTime}");
monitor.AvailableDeviceUpdated += e =>
    Console.WriteLine($"[AvailableUpdated] {e.Identity.ServerName}/{e.Identity.DeviceId} status={e.Device.Status}");

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
