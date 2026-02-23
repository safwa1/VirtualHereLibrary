using System.Buffers;
using System.IO.Pipes;
using System.Text;

namespace vrhero.VirtualHere;

public sealed class VirtualHereIpcClient
{
    private readonly string _serverName;
    private readonly string _pipeName;
    private readonly TimeSpan _connectTimeout;

    public VirtualHereIpcClient(
        string serverName = ".",
        string pipeName = "vhclient",
        TimeSpan? connectTimeout = null)
    {
        _serverName = serverName;
        _pipeName = pipeName;
        _connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(2);
    }

    public async Task<string> SendAsync(string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or whitespace.", nameof(command));
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_connectTimeout);

        await using var pipe = new NamedPipeClientStream(
            _serverName,
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        var timeoutMilliseconds = checked((int)_connectTimeout.TotalMilliseconds);
        await pipe.ConnectAsync(timeoutMilliseconds, linkedCts.Token).ConfigureAwait(false);
        pipe.ReadMode = PipeTransmissionMode.Message;

        var normalized = command.EndsWith('\n') ? command : string.Concat(command, "\n");
        var commandBytes = Encoding.UTF8.GetBytes(normalized);
        await pipe.WriteAsync(commandBytes, linkedCts.Token).ConfigureAwait(false);
        await pipe.FlushAsync(linkedCts.Token).ConfigureAwait(false);

        var pool = ArrayPool<byte>.Shared;
        var rented = pool.Rent(4096);

        try
        {
            using var output = new MemoryStream(4096);
            while (true)
            {
                var bytesRead = await pipe.ReadAsync(rented.AsMemory(), linkedCts.Token).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                output.Write(rented, 0, bytesRead);
                if (pipe.IsMessageComplete)
                {
                    break;
                }
            }

            return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
        }
        finally
        {
            pool.Return(rented);
        }
    }
}
