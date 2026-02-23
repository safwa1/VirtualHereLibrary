using System.Text.RegularExpressions;

namespace vrhero.VirtualHere;

public sealed class UsageTimeManager
{
    public string GetUsageTime(string? raw) => raw ?? string.Empty;
}

public sealed class UsedDevice
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UsedBy { get; set; } = string.Empty;
    public DateTimeOffset? StartTime { get; set; }
    public string UsageTime { get; set; } = string.Empty;
    public string TimeRemaining { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;

    public void UpdateFrom(UsedDevice other)
    {
        DeviceName = other.DeviceName;
        DeviceId = other.DeviceId;
        IpAddress = other.IpAddress;
        UsedBy = other.UsedBy;
        StartTime = other.StartTime;
        UsageTime = other.UsageTime;
        TimeRemaining = other.TimeRemaining;
        ServerName = other.ServerName;
    }
}

public sealed class AvailableDevice
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string UsageTime { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;

    public void UpdateFrom(AvailableDevice other)
    {
        DeviceName = other.DeviceName;
        DeviceId = other.DeviceId;
        Status = other.Status;
        UsageTime = other.UsageTime;
        ServerName = other.ServerName;
    }

    public bool IsInUse()
    {
        return Status.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public sealed class VirtualHereSnapshot
{
    public List<UsedDevice> UsedDevices { get; } = new();
    public List<AvailableDevice> AvailableDevices { get; } = new();
}

public sealed class VirtualHereResponseParser
{
    private static readonly Regex ServerRegex = new(@"^(?<name>.+)\((?<ip>[^:]+):(?<port>\d+)\)$", RegexOptions.Compiled);
    private static readonly Regex DeviceRegex = new(@"^\s*\-\-\>\s*(?<name>.+?)\s*\((?<id>.+?)\)(?<tail>.*)$", RegexOptions.Compiled);
    private readonly UsageTimeManager _usageTimeManager;

    public VirtualHereResponseParser(UsageTimeManager usageTimeManager)
    {
        _usageTimeManager = usageTimeManager;
    }

    public VirtualHereSnapshot ParseAll(string data)
    {
        var snapshot = new VirtualHereSnapshot();
        if (string.IsNullOrWhiteSpace(data))
        {
            return snapshot;
        }

        string serverName = string.Empty;
        string ipAddress = string.Empty;

        var lines = data.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var serverMatch = ServerRegex.Match(line.Trim());
            if (serverMatch.Success)
            {
                serverName = serverMatch.Groups["name"].Value.Trim();
                ipAddress = serverMatch.Groups["ip"].Value.Trim();
                continue;
            }

            var deviceMatch = DeviceRegex.Match(line);
            if (!deviceMatch.Success)
            {
                continue;
            }

            var name = deviceMatch.Groups["name"].Value.Trim();
            var id = deviceMatch.Groups["id"].Value.Trim();
            var tail = deviceMatch.Groups["tail"].Value.Trim();

            if (tail.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                snapshot.UsedDevices.Add(new UsedDevice
                {
                    DeviceName = name,
                    DeviceId = id,
                    IpAddress = ipAddress,
                    UsedBy = tail,
                    UsageTime = _usageTimeManager.GetUsageTime(tail),
                    ServerName = serverName
                });
            }
            else
            {
                snapshot.AvailableDevices.Add(new AvailableDevice
                {
                    DeviceName = name,
                    DeviceId = id,
                    Status = string.IsNullOrWhiteSpace(tail) ? "available" : tail,
                    UsageTime = _usageTimeManager.GetUsageTime(tail),
                    ServerName = serverName
                });
            }
        }

        return snapshot;
    }
}
