using System.Buffers.Binary;

namespace GpsReceiver;

public sealed record Fix(DateTimeOffset DeviceUtc, byte Priority, double Longitude, double Latitude,
    ushort AltitudeMeters, ushort HeadingDegrees, byte Satellites, ushort RawSpeedKph,
    bool PositionUsable, ushort? SpeedKph, int EventIoId, Dictionary<int, string> IoHex);

public static class Protocol
{
    public static ushort Crc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
        }
        return crc;
    }

    public static List<Fix> Decode(byte[] frame)
    {
        if (frame.Length < 15 || BinaryPrimitives.ReadUInt32BigEndian(frame) != 0) throw new InvalidDataException("Invalid preamble");
        var length = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(4));
        if (length != frame.Length - 12) throw new InvalidDataException("Invalid length");
        if (BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(frame.Length - 4)) != Crc(frame.AsSpan(8, (int)length)))
            throw new InvalidDataException("CRC mismatch");
        var r = new Reader(frame[8..^4]);
        var codec = r.Number(1);
        if (codec is not (8 or 142)) throw new InvalidDataException("Unsupported codec");
        int width = codec == 8 ? 1 : 2;
        var count = r.Number(1);
        if (count == 0) throw new InvalidDataException("Empty packet");
        var fixes = new List<Fix>();
        for (ulong index = 0; index < count; index++)
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(checked((long)r.Number(8)));
            var priority = (byte)r.Number(1);
            var lon = unchecked((int)(uint)r.Number(4)) / 10000000d;
            var lat = unchecked((int)(uint)r.Number(4)) / 10000000d;
            var altitude = (ushort)r.Number(2);
            var heading = (ushort)r.Number(2);
            var satellites = (byte)r.Number(1);
            var speed = (ushort)r.Number(2);
            var eventId = (int)r.Number(width);
            var total = r.Number(width);
            var io = new Dictionary<int, string>();
            ulong seen = 0;
            foreach (var size in new[] { 1, 2, 4, 8 })
            {
                var n = r.Number(width);
                for (ulong j = 0; j < n; j++)
                {
                    var id = (int)r.Number(width);
                    if (!io.TryAdd(id, Convert.ToHexString(r.Bytes(size)))) throw new InvalidDataException("Duplicate IO ID");
                    seen++;
                }
            }
            if (width == 2)
            {
                var n = r.Number(2);
                for (ulong j = 0; j < n; j++)
                {
                    var id = (int)r.Number(2);
                    var size = (int)r.Number(2);
                    if (!io.TryAdd(id, Convert.ToHexString(r.Bytes(size)))) throw new InvalidDataException("Duplicate IO ID");
                    seen++;
                }
            }
            if (seen != total) throw new InvalidDataException("IO count mismatch");
            var usable = satellites > 0 && lat is >= -90 and <= 90 && lon is >= -180 and <= 180 && heading <= 360;
            fixes.Add(new(timestamp, priority, lon, lat, altitude, heading, satellites, speed, usable,
                usable ? speed : null, eventId, io));
        }
        if (r.Number(1) != count || !r.End) throw new InvalidDataException("Record count or trailing data mismatch");
        return fixes;
    }

    private sealed class Reader(byte[] data)
    {
        private int offset;
        public bool End => offset == data.Length;
        public byte[] Bytes(int n)
        {
            if (n < 0 || n > data.Length - offset) throw new InvalidDataException("Truncated record");
            var result = data.AsSpan(offset, n).ToArray(); offset += n; return result;
        }
        public ulong Number(int n)
        {
            ulong result = 0;
            foreach (var b in Bytes(n)) result = (result << 8) | b;
            return result;
        }
    }
}
