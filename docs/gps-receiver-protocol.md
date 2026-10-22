# GPS Receiver protocol and scope

The explicit experiment scope overrides the broader Fleet_Tracking_Codex_Spec_v2.0.md: standalone .NET 8 TCP receiver, plain-text durable capture, simulator, no broker/database/UI. No existing simulator or source project was present.

Primary protocol reference: [Teltonika Data Sending Protocols](https://wiki.teltonika-gps.com/view/Teltonika_Data_Sending_Protocols), Codec 8 and Codec 8 Extended TCP sections, checked 2026-10-22. Tests and simulator use the official first Codec 8 example (known CRC C7CF); synthetic Codec 8E coverage is separately labeled.

Identity is a two-byte big-endian length followed by 15 ASCII digits. Receiver returns binary 01 for accepted or 00 for rejected identities. Empty allowlist permits any syntactically valid identity for initial simulator testing. TCP frames use four zero preamble bytes, four-byte big-endian data length, codec/count/records/count, and four-byte CRC field. CRC-16/IBM (initial zero, polynomial A001 reflected) covers only the data field. Accepted count is returned as four-byte big-endian integer after successful parse and capture fsync. Invalid frames close without a success ACK.

Read loops handle arbitrary TCP fragmentation and consecutive frames. Partial inbound bytes are recorded immediately. Each session has its own UTF-8 JSON-lines .txt file. Every record retains device UTC, packet server UTC, signed coordinate scaling, raw GPS speed, satellite count, heading, altitude, event IO ID, and raw IO bytes. Invalid/basic unusable GPS has nullable interpreted speed. No engine or sensor meaning is invented. Retransmissions are retained.

Resource controls: bounded concurrent sessions, bounded frame allocation, per-part read deadline, container non-root user, read-only image and persistent host bind mount. Capture files contain sensitive data and are not committed. Storage has no retention policy or quota automation yet. A capture write failure prevents data ACK; failure details are restricted in console output. Inbound malformed data remains in rx entries for diagnostics.

See [Persian deployment guide](gps-receiver-fa.md) for complete operator commands and limitations. Tests run with `dotnet run --project tests/GpsReceiver.Tests -c Release`. Container build remains unverified until a Docker-capable host is available.
