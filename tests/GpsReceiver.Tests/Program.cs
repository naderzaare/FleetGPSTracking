using GpsReceiver;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

// Official Codec 8 example: invalid GPS, five IO values, known CRC C7CF.
var fixture = Convert.FromHexString("000000000000003608010000016B40D8EA30010000000000000000000000000000000105021503010101425E0F01F10000601A014E0000000000000000010000C7CF");
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
var records = Protocol.Decode(fixture);
Check(records.Count == 1 && records[0].IoHex.Count == 5 && records[0].SpeedKph is null, "official Codec8 fixture / invalid GPS");
Check(Protocol.Crc(Encoding.ASCII.GetBytes("123456789")) == 0xBB3D, "independent CRC check vector");
void Reject(byte[] data, string name)
{
    try { Protocol.Decode(data); throw new Exception("Accepted " + name); }
    catch (InvalidDataException) { Console.WriteLine("PASS " + name); }
}
var corrupt = fixture.ToArray(); corrupt[^1] ^= 1; Reject(corrupt, "bad CRC");
Reject(fixture[..^1], "truncated frame");
byte[] Wrap(byte[] payload)
{
    var frame = new byte[payload.Length + 12];
    BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(4), payload.Length);
    payload.CopyTo(frame, 8);
    BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(frame.Length - 4), Protocol.Crc(payload)); return frame;
}
// Synthetic extended record with signed coordinates and variable IO.
var payload = Convert.FromHexString("8E010000016B40D8EA3001FFFFFFFFFFFFFFFF000000000500140000000100000000000000000001012C0003AABBCC01");
var extended = Wrap(payload);
Check(Protocol.Decode(extended)[0].IoHex[300] == "AABBCC", "Codec8E variable IO");
payload[^1] = 2; Reject(Wrap(payload), "record count mismatch with valid CRC");
var directory = Path.Combine(Path.GetTempPath(), "gps-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
var receiver = new Receiver(new Settings(port, directory, IdleSeconds: 2));
async Task Session(bool malformed = false)
{
    using var client = new TcpClient();
    var connect = client.ConnectAsync(IPAddress.Loopback, port);
    var server = await listener.AcceptTcpClientAsync(); await connect;
    var serving = receiver.ServeAsync(server, CancellationToken.None);
    var stream = client.GetStream();
    foreach (var b in new byte[] { 0, 15 }.Concat(Encoding.ASCII.GetBytes("000000000000000")))
        await stream.WriteAsync(new byte[] { b });
    var answer = new byte[1]; await stream.ReadExactlyAsync(answer);
    Check(answer[0] == 1, "fragmented handshake");
    if (malformed)
    {
        await stream.WriteAsync(corrupt);
        Check(await stream.ReadAsync(answer) == 0, "malformed frame closes without ACK");
    }
    else
    {
        foreach (var b in fixture) await stream.WriteAsync(new byte[] { b });
        var ack = new byte[4]; await stream.ReadExactlyAsync(ack);
        Check(BinaryPrimitives.ReadInt32BigEndian(ack) == 1, "fragmented packet ACK");
        Check(Directory.GetFiles(directory).Any(p => {
            using var file = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file);
            return reader.ReadToEnd().Contains("\"kind\":\"packet\"");
        }), "capture readable before ACK");
        await stream.WriteAsync(extended.Concat(fixture).ToArray());
        var acks = new byte[8]; await stream.ReadExactlyAsync(acks);
        Check(BinaryPrimitives.ReadInt32BigEndian(acks) == 1 && BinaryPrimitives.ReadInt32BigEndian(acks.AsSpan(4)) == 1, "coalesced frames ACKs");
    }
    client.Close(); await serving;
}
await Session(); await Session(true); listener.Stop();
Check(Directory.GetFiles(directory).Length == 2, "captures persist across connections");
Console.WriteLine("Capture test directory: " + directory);
