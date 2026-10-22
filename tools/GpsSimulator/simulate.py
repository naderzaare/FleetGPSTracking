"""Synthetic identity; official Codec8 invalid-GPS fixture. Python standard library only."""
import argparse
import socket
import struct

PACKET = bytes.fromhex('000000000000003608010000016B40D8EA30010000000000000000000000000000000105021503010101425E0F01F10000601A014E0000000000000000010000C7CF')

def exact(sock, count):
    data = b''
    while len(data) < count:
        chunk = sock.recv(count - len(data))
        if not chunk:
            raise RuntimeError('Receiver closed before ACK')
        data += chunk
    return data

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', default='127.0.0.1')
    parser.add_argument('--port', type=int, default=5027)
    parser.add_argument('--fragment', action='store_true')
    args = parser.parse_args()
    with socket.create_connection((args.host, args.port), timeout=10) as sock:
        for packet, expected in [(b'\x00\x0f' + b'000000000000000', b'\x01'), (PACKET, struct.pack('>I', 1))]:
            for chunk in ([bytes([b]) for b in packet] if args.fragment else [packet]):
                sock.sendall(chunk)
            if exact(sock, len(expected)) != expected:
                raise RuntimeError('Unexpected ACK')
    print('PASS: handshake and one-record ACK. Fixture has no valid GPS fix.')
