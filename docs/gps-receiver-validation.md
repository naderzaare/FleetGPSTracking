# Receiver validation

Executed on Windows with .NET SDK 8.0.426; no external test packages required.

Passed:

- Official Codec 8 packet with known CRC and five IO values; invalid GPS retains raw speed while interpreted speed is null.
- CRC-16/IBM independent `123456789` check vector.
- CRC corruption, frame truncation and mismatched trailing record count rejection.
- Synthetic Codec 8 Extended variable IO decoding.
- Loopback TCP identity and AVL packet sent one byte at a time.
- Multiple frames in one write with separate four-byte ACKs.
- Capture file readable and containing decoded packet before ACK is observed.
- Malformed CRC closes connection without a successful ACK.
- Separate capture files remain after connections close.
- Python simulator against the actual receiver entry point with `--fragment`: successful identity and one-record ACK.

Commands:

```text
dotnet run --project tests/GpsReceiver.Tests -c Release
dotnet run --project src/GpsReceiver -c Release
python tools/GpsSimulator/simulate.py --fragment
```

The final harness was built with `UseAppHost=false` and run as a DLL after a failed test process temporarily held the Windows executable. All final assertions passed with exit code zero. Release build completed successfully. `git diff --check` passed.

Not verified here: Docker image build, Linux signal handling and bind-mount permissions, actual container replacement, full disk/power loss behavior, real device firmware/carrier/offline replay, production load. Docker CLI and hardware were unavailable. Operator acceptance commands are in `gps-receiver-fa.md`. No push or deployment was performed.
