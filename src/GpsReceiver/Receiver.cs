using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GpsReceiver;

public sealed record Settings(int Port, string CaptureDirectory, int IdleSeconds = 180,
    int MaxConnections = 100, int MaxFrameBytes = 65536, string[]? AllowedImeis = null);

public sealed class Receiver(Settings settings)
{
    public async Task RunAsync(CancellationToken stop)
    {
        Directory.CreateDirectory(settings.CaptureDirectory);
        // Fail startup if the mounted directory is not writable.
        var probe = Path.Combine(settings.CaptureDirectory, $".probe-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(probe, "", stop); File.Delete(probe);
        var listener = new TcpListener(IPAddress.Any, settings.Port);
        listener.Start();
        Console.WriteLine($"GPS Receiver listening TCP {settings.Port}");
        var active = new HashSet<Task>();
        try
        {
            while (!stop.IsCancellationRequested)
            {
                foreach (var done in active.Where(t => t.IsCompleted).ToArray()) { await done; active.Remove(done); }
                if (active.Count >= settings.MaxConnections)
                {
                    var done = await Task.WhenAny(active); await done; active.Remove(done);
                }
                var client = await listener.AcceptTcpClientAsync(stop);
                active.Add(ServeAsync(client, stop));
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { listener.Stop(); await Task.WhenAll(active); }
    }

    public async Task ServeAsync(TcpClient client, CancellationToken stop)
    {
        using (client)
        {
            var id = Guid.NewGuid().ToString("N");
            string? imei = null;
            Console.WriteLine($"{DateTimeOffset.UtcNow:O} connected session={id}");
            try
            {
                using var capture = new Capture(Path.Combine(settings.CaptureDirectory, $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{id}.txt"));
                var stream = client.GetStream();
                async Task<byte[]> Read(int count)
                {
                    var bytes = new byte[count]; int offset = 0;
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
                    deadline.CancelAfter(TimeSpan.FromSeconds(settings.IdleSeconds));
                    while (offset < count)
                    {
                        var n = await stream.ReadAsync(bytes.AsMemory(offset), deadline.Token);
                        if (n == 0) throw new EndOfStreamException();
                        // Preserve every received chunk, including partial/malformed frames.
                        capture.Write(new { kind = "rx", serverUtc = DateTimeOffset.UtcNow, hex = Convert.ToHexString(bytes.AsSpan(offset, n)) });
                        offset += n;
                    }
                    return bytes;
                }
                async Task Send(byte[] bytes)
                {
                    await stream.WriteAsync(bytes, stop);
                    capture.Write(new { kind = "tx", serverUtc = DateTimeOffset.UtcNow, hex = Convert.ToHexString(bytes) });
                }
                capture.Write(new { kind = "connected", serverUtc = DateTimeOffset.UtcNow, session = id, remote = client.Client.RemoteEndPoint?.ToString() });
                var prefix = await Read(2);
                if (BinaryPrimitives.ReadUInt16BigEndian(prefix) != 15) { await Send([0]); throw new InvalidDataException("IMEI length"); }
                var identity = await Read(15);
                imei = Encoding.ASCII.GetString(identity);
                if (identity.Any(b => b < '0' || b > '9') ||
                    (settings.AllowedImeis is { Length: > 0 } && !settings.AllowedImeis.Contains(imei)))
                { await Send([0]); throw new InvalidDataException("Identity rejected"); }
                capture.Write(new { kind = "identity", serverUtc = DateTimeOffset.UtcNow, imei });
                await Send([1]);
                while (!stop.IsCancellationRequested)
                {
                    var header = await Read(8);
                    var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
                    if (BinaryPrimitives.ReadUInt32BigEndian(header) != 0 || length < 3 || length > settings.MaxFrameBytes)
                        throw new InvalidDataException("Invalid frame header");
                    var body = await Read(checked((int)length + 4));
                    var frame = header.Concat(body).ToArray();
                    var received = DateTimeOffset.UtcNow;
                    var fixes = Protocol.Decode(frame);
                    capture.Write(new { kind = "packet", serverUtc = received, imei, codec = frame[8], rawHex = Convert.ToHexString(frame), records = fixes });
                    // Write + fsync completed before accepting records. Retransmissions remain separate captures.
                    var ack = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(ack, fixes.Count);
                    await Send(ack);
                    Console.WriteLine($"{received:O} accepted session={id} codec={frame[8]:X2} records={fixes.Count} bytes={frame.Length}");
                }
            }
            catch (Exception e) when (e is InvalidDataException or IOException or SocketException or OperationCanceledException or ArgumentException or OverflowException or UnauthorizedAccessException)
            { Console.WriteLine($"{DateTimeOffset.UtcNow:O} closed session={id} reason={e.GetType().Name}" +
                (e is InvalidDataException ? $" detail={e.Message}" : "")); }
            finally { Console.WriteLine($"{DateTimeOffset.UtcNow:O} disconnected session={id}"); }
        }
    }

    private sealed class Capture : IDisposable
    {
        private readonly FileStream file;
        public Capture(string path) => file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        public void Write(object entry)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
            file.Write(bytes); file.Flush(flushToDisk: true);
        }
        public void Dispose() => file.Dispose();
    }
}
