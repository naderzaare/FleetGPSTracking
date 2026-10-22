using GpsReceiver;

int Number(string key, int fallback, int min, int max)
{
    var raw = Environment.GetEnvironmentVariable(key);
    var value = raw is null ? fallback : int.Parse(raw);
    return value >= min && value <= max ? value : throw new ArgumentOutOfRangeException(key);
}
var settings = new Settings(Number("GPS_PORT", 5027, 1, 65535),
    Environment.GetEnvironmentVariable("GPS_CAPTURE_DIR") ?? "captures",
    Number("GPS_IDLE_SECONDS", 180, 1, 86400), Number("GPS_MAX_CONNECTIONS", 100, 1, 5000),
    Number("GPS_MAX_FRAME_BYTES", 65536, 64, 1048576),
    Environment.GetEnvironmentVariable("GPS_ALLOWED_IMEIS")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
using var term = OperatingSystem.IsWindows() ? null : System.Runtime.InteropServices.PosixSignalRegistration.Create(
    System.Runtime.InteropServices.PosixSignal.SIGTERM, context => { context.Cancel = true; shutdown.Cancel(); });
await new Receiver(settings).RunAsync(shutdown.Token);
