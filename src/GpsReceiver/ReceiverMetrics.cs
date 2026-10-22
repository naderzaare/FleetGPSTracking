using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GpsReceiver;

// Fixed metric names/labels only: no device identifiers, addresses, coordinates or session IDs.
public sealed class ReceiverMetrics
{
    private readonly object gate = new();
    private readonly long started = Stopwatch.GetTimestamp();
    private static readonly double[] Bounds = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];
    private readonly long[] buckets = new long[Bounds.Length];
    private long connections, active, handshakes, receivedBytes, frames, storedPackets, storedRecords, invalidPositions, acks;
    private long protocolErrors, storageErrors, networkErrors, timeouts, observations;
    private double durationSum, lastStored;
    private bool listening;

    public void Listening(bool value) { lock (gate) listening = value; }
    public bool IsListening { get { lock (gate) return listening; } }
    public void Connected() { lock (gate) { connections++; active++; } }
    public void Disconnected() { lock (gate) active--; }
    public void Handshake() { lock (gate) handshakes++; }
    public void Received(int bytes) { lock (gate) receivedBytes += bytes; }
    public void Frame() { lock (gate) frames++; }
    public void Ack() { lock (gate) acks++; }
    public void Stored(IReadOnlyCollection<Fix> fixes)
    {
        lock (gate)
        {
            storedPackets++; storedRecords += fixes.Count;
            invalidPositions += fixes.Count(f => !f.PositionUsable);
            lastStored = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
        }
    }
    public void Error(string reason)
    {
        lock (gate)
        {
            switch (reason)
            {
                case "protocol": protocolErrors++; break;
                case "storage": storageErrors++; break;
                case "network": networkErrors++; break;
                case "timeout": timeouts++; break;
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }
    }
    public void Observe(double seconds)
    {
        lock (gate)
        {
            observations++; durationSum += seconds;
            for (var i = 0; i < Bounds.Length; i++) if (seconds <= Bounds[i]) buckets[i]++;
        }
    }

    public string Render()
    {
        lock (gate)
        {
            var output = new StringBuilder();
            static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
            void Metric(string name, string type, string help, double value)
            {
                output.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
                output.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');
                output.Append(name).Append(' ').Append(F(value)).Append('\n');
            }
            Metric("gps_receiver_listener_ready", "gauge", "TCP listener has started; not a storage or device health guarantee.", listening ? 1 : 0);
            Metric("gps_receiver_uptime_seconds", "gauge", "Seconds since metrics initialization.", Stopwatch.GetElapsedTime(started).TotalSeconds);
            using (var process = Process.GetCurrentProcess())
            {
                Metric("gps_receiver_process_working_set_bytes", "gauge", "Resident memory of this receiver process; not the entire host.", process.WorkingSet64);
                Metric("gps_receiver_process_cpu_seconds_total", "counter", "Total user and kernel CPU seconds of this receiver process.", process.TotalProcessorTime.TotalSeconds);
            }
            Metric("gps_receiver_connections_total", "counter", "Accepted TCP connections including unidentified clients.", connections);
            Metric("gps_receiver_connections_active", "gauge", "Currently open TCP connections including handshakes.", active);
            Metric("gps_receiver_handshakes_total", "counter", "Accepted identity handshakes with response written.", handshakes);
            Metric("gps_receiver_received_bytes_total", "counter", "Bytes read from GPS TCP clients including malformed input.", receivedBytes);
            Metric("gps_receiver_frames_received_total", "counter", "Complete bounded AVL frames received before validation.", frames);
            Metric("gps_receiver_packets_stored_total", "counter", "Validated packet captures successfully flushed to disk; includes retransmissions.", storedPackets);
            Metric("gps_receiver_records_stored_total", "counter", "Records in successfully flushed packet captures; includes retransmissions.", storedRecords);
            Metric("gps_receiver_invalid_positions_total", "counter", "Stored records failing basic GPS usability checks; not protocol errors.", invalidPositions);
            Metric("gps_receiver_data_acks_total", "counter", "Four byte data ACKs written to the TCP stream; not proof client received them.", acks);
            Metric("gps_receiver_last_packet_stored_timestamp_seconds", "gauge", "Server UTC Unix time of last flushed packet; zero before first packet.", lastStored);
            output.Append("# HELP gps_receiver_errors_total Session failures by fixed reason; normal EOF and shutdown excluded.\n# TYPE gps_receiver_errors_total counter\n");
            foreach (var (reason, value) in new[] { ("protocol", protocolErrors), ("storage", storageErrors), ("network", networkErrors), ("timeout", timeouts) })
                output.Append("gps_receiver_errors_total{reason=\"").Append(reason).Append("\"} ").Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            const string histogram = "gps_receiver_packet_processing_seconds";
            output.Append("# HELP ").Append(histogram).Append(" Validation and durable packet write duration for complete frames; includes failed attempts, excludes socket reads and ACK.\n# TYPE ").Append(histogram).Append(" histogram\n");
            for (var i = 0; i < Bounds.Length; i++)
                output.Append(histogram).Append("_bucket{le=\"").Append(F(Bounds[i])).Append("\"} ").Append(buckets[i].ToString(CultureInfo.InvariantCulture)).Append('\n');
            output.Append(histogram).Append("_bucket{le=\"+Inf\"} ").Append(observations.ToString(CultureInfo.InvariantCulture)).Append('\n');
            output.Append(histogram).Append("_sum ").Append(F(durationSum)).Append('\n');
            output.Append(histogram).Append("_count ").Append(observations.ToString(CultureInfo.InvariantCulture)).Append('\n');
            return output.ToString();
        }
    }
}
