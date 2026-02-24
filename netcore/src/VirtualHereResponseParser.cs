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

    public UsedDevice(string deviceName, string deviceId, string ipAddress, string usedBy, DateTime startTime, TimeSpan usageTime, string timeRemaining, string serverName)
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

    public bool IsInUse() =>
        Status.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0
        || Status.IndexOf("bound", StringComparison.OrdinalIgnoreCase) >= 0
        || Status.IndexOf("used", StringComparison.OrdinalIgnoreCase) >= 0;

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
        var value = DeviceId.Split('.')[^1];
        return int.Parse(value, CultureInfo.InvariantCulture);
    }
}

public sealed class ClientInfo
{
    public string ClientIp { get; set; } = string.Empty;
    public int ConnectionId { get; set; }
    public string ConnectionType { get; set; } = string.Empty;
}

public sealed class VirtualHereSnapshot
{
    public List<UsedDevice> UsedDevices { get; } = new();
    public List<AvailableDevice> AvailableDevices { get; } = new();
    public List<ClientInfo> Clients { get; } = new();
}

public sealed class VirtualHereResponseParser
{
    private static readonly Regex ServerRegex = new(@"^(?<name>.+)\((?<ip>[^:]+):(?<port>\d+)\)$", RegexOptions.Compiled);
    private static readonly Regex DeviceLineRegex = new(@"^\s*\-\-\>\s*(?<body>.+)$", RegexOptions.Compiled);
    private static readonly Regex ParenthesisIdRegex = new(@"\((?<id>[^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex VendorProductRegex = new(@"\[(?<vendor>[0-9a-fA-F]{4}):(?<product>[0-9a-fA-F]{4})\]", RegexOptions.Compiled);
    private static readonly Regex AddressRegex = new(@"(?:\bat\s+address\s+|\baddress\s+)(?<address>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UsedByRegex = new(@"(?:used\s+by|bound\s+to|by)\s*(?<who>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TimeRemainingRegex = new(@"(?:remaining|left)\s*[:=]\s*(?<remaining>[^,;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ClientLineRegex = new(@"(?:connection\s+(?<cid>\d+).*(?<ip>\d{1,3}(?:\.\d{1,3}){3}))|(?:(?<ip2>\d{1,3}(?:\.\d{1,3}){3}).*connection\s+(?<cid2>\d+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        var lines = data.Split('
');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('');
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

            if (TryParseClientLine(line, out var clientInfo))
            {
                snapshot.Clients.Add(clientInfo);
                continue;
            }

            if (!TryParseDeviceLine(line, out var name, out var id, out var tail, out var usedBy))
            {
                continue;
            }

            var usageTime = _usageTimeManager.ParseUsageTime(tail);
            if (IsUsedState(tail))
            {
                snapshot.UsedDevices.Add(new UsedDevice(
                    deviceName: name,
                    deviceId: id,
                    ipAddress: ipAddress,
                    usedBy: string.IsNullOrWhiteSpace(usedBy) ? tail : usedBy,
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

    private static bool TryParseClientLine(string line, out ClientInfo client)
    {
        client = new ClientInfo();
        var match = ClientLineRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var ip = match.Groups["ip"].Success ? match.Groups["ip"].Value : match.Groups["ip2"].Value;
        var connectionText = match.Groups["cid"].Success ? match.Groups["cid"].Value : match.Groups["cid2"].Value;

        if (string.IsNullOrWhiteSpace(ip))
        {
            return false;
        }

        _ = int.TryParse(connectionText, out var connectionId);

        client = new ClientInfo
        {
            ClientIp = ip.Trim(),
            ConnectionId = connectionId,
            ConnectionType = line.IndexOf("tcp", StringComparison.OrdinalIgnoreCase) >= 0 ? "TCP" : "VirtualHere IPC"
        };
        return true;
    }

    private static bool TryParseDeviceLine(string line, out string name, out string id, out string tail, out string usedBy)
    {
        name = string.Empty;
        id = string.Empty;
        tail = string.Empty;
        usedBy = string.Empty;

        var lineMatch = DeviceLineRegex.Match(line);
        if (!lineMatch.Success)
        {
            return false;
        }

        var body = lineMatch.Groups["body"].Value.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var idMatch = ParenthesisIdRegex.Match(body);
        if (idMatch.Success)
        {
            id = idMatch.Groups["id"].Value.Trim();
        }

        var vpMatch = VendorProductRegex.Match(body);
        var vendorProduct = vpMatch.Success ? $"{vpMatch.Groups["vendor"].Value}:{vpMatch.Groups["product"].Value}" : string.Empty;

        var addressMatch = AddressRegex.Match(body);
        var address = addressMatch.Success ? addressMatch.Groups["address"].Value : string.Empty;

        name = ExtractName(body);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Unknown Device";
        }

        if (idMatch.Success)
        {
            var tailStart = idMatch.Index + idMatch.Length;
            tail = tailStart < body.Length ? body[tailStart..].Trim(' ', ',', ';') : string.Empty;
        }
        else
        {
            tail = body;
        }

        var usedByMatch = UsedByRegex.Match(tail);
        if (usedByMatch.Success)
        {
            usedBy = usedByMatch.Groups["who"].Value.Trim();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            if (!string.IsNullOrWhiteSpace(vendorProduct) && !string.IsNullOrWhiteSpace(address))
            {
                id = $"{vendorProduct}.{address}";
            }
            else if (!string.IsNullOrWhiteSpace(vendorProduct))
            {
                id = vendorProduct;
            }
            else
            {
                id = name;
            }
        }

        return true;
    }

    private static string ExtractName(string body)
    {
        var parenIndex = body.IndexOf('(');
        var bracketIndex = body.IndexOf('[');

        var stop = -1;
        if (parenIndex >= 0 && bracketIndex >= 0)
        {
            stop = Math.Min(parenIndex, bracketIndex);
        }
        else if (parenIndex >= 0)
        {
            stop = parenIndex;
        }
        else if (bracketIndex >= 0)
        {
            stop = bracketIndex;
        }

        if (stop <= 0)
        {
            return body.Trim();
        }

        return body[..stop].Trim();
    }

    private static bool IsUsedState(string tail)
    {
        return tail.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0
               || tail.IndexOf("used by", StringComparison.OrdinalIgnoreCase) >= 0
               || tail.IndexOf("bound", StringComparison.OrdinalIgnoreCase) >= 0
               || tail.IndexOf("currently used", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ParseTimeRemaining(string tail)
    {
        var match = TimeRemainingRegex.Match(tail);
        return match.Success ? match.Groups["remaining"].Value.Trim() : string.Empty;
    }
}
