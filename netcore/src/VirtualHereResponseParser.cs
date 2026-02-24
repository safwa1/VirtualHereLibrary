using System.Globalization;
using System.Text.RegularExpressions;

namespace vrhero.VirtualHere;

public sealed class UsageTimeManager
{
    private static readonly Regex DurationRegex = new(@"(?<duration>\d{1,3}:\d{2}:\d{2}|\d{1,2}:\d{2})", RegexOptions.Compiled);

    public TimeSpan ParseUsageTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TimeSpan.Zero;
        }

        var match = DurationRegex.Match(raw);
        if (!match.Success)
        {
            return TimeSpan.Zero;
        }

        var value = match.Groups["duration"].Value;
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var usage))
        {
            return usage;
        }

        if (TimeSpan.TryParse(value, out usage))
        {
            return usage;
        }

        return TimeSpan.Zero;
    }

    public DateTime GetStartTime(TimeSpan usageTime)
    {
        return DateTime.Now - usageTime;
    }
}

public sealed partial record UsedDevice
{
    public string DeviceName { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string UsedBy { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public TimeSpan UsageTime { get; set; }

    public string TimeRemaining { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    public UsedDevice()
    {
    }

    public UsedDevice(
        string deviceName,
        string deviceId,
        string ipAddress,
        string usedBy,
        DateTime startTime,
        TimeSpan usageTime,
        string timeRemaining,
        string serverName)
    {
        DeviceName = deviceName;
        DeviceId = deviceId;
        IpAddress = ipAddress;
        UsedBy = usedBy;
        StartTime = startTime;
        UsageTime = usageTime;
        TimeRemaining = timeRemaining;
        ServerName = serverName;
    }

    public string FullDeviceName(string splitter = " ") => $"{DeviceName}{splitter}({DeviceId})";

    public UsedDevice UpdateFrom(UsedDevice other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new UsedDevice(
            deviceName: other.DeviceName != DeviceName ? other.DeviceName : DeviceName,
            deviceId: other.DeviceId != DeviceId ? other.DeviceId : DeviceId,
            ipAddress: other.IpAddress != IpAddress ? other.IpAddress : IpAddress,
            usedBy: other.UsedBy != UsedBy ? other.UsedBy : UsedBy,
            startTime: other.StartTime != StartTime ? other.StartTime : StartTime,
            usageTime: other.UsageTime != UsageTime ? other.UsageTime : UsageTime,
            timeRemaining: other.TimeRemaining != TimeRemaining ? other.TimeRemaining : TimeRemaining,
            serverName: other.ServerName != ServerName ? other.ServerName : ServerName);
    }
}

public sealed partial record AvailableDevice
{
    public string DeviceName { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public TimeSpan UsageTime { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public AvailableDevice()
    {
    }

    public AvailableDevice(string deviceName, string deviceId, string status, TimeSpan usageTime, string serverName)
    {
        DeviceName = deviceName;
        DeviceId = deviceId;
        Status = status;
        UsageTime = usageTime;
        ServerName = serverName;
    }

    public bool IsInUse() => string.Equals(Status, "In Use", StringComparison.OrdinalIgnoreCase);

    public AvailableDevice UpdateFrom(AvailableDevice other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new AvailableDevice(
            deviceName: other.DeviceName != DeviceName ? other.DeviceName : DeviceName,
            deviceId: other.DeviceId != DeviceId ? other.DeviceId : DeviceId,
            status: other.Status != Status ? other.Status : Status,
            usageTime: other.UsageTime != UsageTime ? other.UsageTime : UsageTime,
            serverName: other.ServerName != ServerName ? other.ServerName : ServerName);
    }

    public string FullDeviceName(string splitter = " ") => $"{DeviceName}{splitter}({DeviceId})";

    public string UsageTimeAsString()
    {
        var value = UsageTime.ToString("g", CultureInfo.InvariantCulture);
        var dotIndex = value.IndexOf('.');
        return dotIndex != -1 ? value[..dotIndex] : value;
    }

    public int GetAddress()
    {
        var value = DeviceId.Split(".")[^1];
        return int.Parse(value, CultureInfo.InvariantCulture);
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
    private static readonly Regex TimeRemainingRegex = new(@"(?:remaining|left)\s*[:=]\s*(?<remaining>[^,;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly UsageTimeManager _usageTimeManager;

    public VirtualHereResponseParser(UsageTimeManager usageTimeManager)
    {
        _usageTimeManager = usageTimeManager ?? throw new ArgumentNullException(nameof(usageTimeManager));
    }

    public VirtualHereSnapshot ParseAll(string data)
    {
        var snapshot = new VirtualHereSnapshot();
        if (string.IsNullOrWhiteSpace(data))
        {
            return snapshot;
        }

        var serverName = string.Empty;
        var ipAddress = string.Empty;

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
            var usageTime = _usageTimeManager.ParseUsageTime(tail);

            if (tail.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                snapshot.UsedDevices.Add(new UsedDevice(
                    deviceName: name,
                    deviceId: id,
                    ipAddress: ipAddress,
                    usedBy: tail,
                    startTime: _usageTimeManager.GetStartTime(usageTime),
                    usageTime: usageTime,
                    timeRemaining: ParseTimeRemaining(tail),
                    serverName: serverName));
            }
            else
            {
                snapshot.AvailableDevices.Add(new AvailableDevice(
                    deviceName: name,
                    deviceId: id,
                    status: string.IsNullOrWhiteSpace(tail) ? "Available" : tail,
                    usageTime: usageTime,
                    serverName: serverName));
            }
        }

        return snapshot;
    }

    private static string ParseTimeRemaining(string tail)
    {
        var match = TimeRemainingRegex.Match(tail);
        return match.Success ? match.Groups["remaining"].Value.Trim() : string.Empty;
    }
}
