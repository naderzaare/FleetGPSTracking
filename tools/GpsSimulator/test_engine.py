import socket
import struct
import threading
import time
import unittest
from unittest.mock import patch

from engine import Settings, Simulator, make_packet
from simulate import exact


class EngineTests(unittest.TestCase):
    def test_settings(self):
        for settings in [Settings(devices=0), Settings(devices=101), Settings(interval=0),
                         Settings(interval=float('nan')), Settings(port=0), Settings(host='')]:
            with self.assertRaises(ValueError):
                settings.validate()

    def test_packet_timestamp_and_crc(self):
        with patch('engine.time.time_ns', return_value=1_800_000_000_000_000_000):
            packet = make_packet()
        self.assertEqual(struct.unpack_from('>Q', packet, 10)[0], 1_800_000_000_000)
        # Independent polynomial division in normal (non-reflected) bit order.
        crc = 0
        for byte in packet[8:-4]:
            reflected = int(f'{byte:08b}'[::-1], 2)
            crc ^= reflected << 8
            for _ in range(8):
                crc = ((crc << 1) ^ (0x8005 if crc & 0x8000 else 0)) & 0xffff
        crc = int(f'{crc:016b}'[::-1], 2)
        self.assertEqual(struct.unpack('>I', packet[-4:])[0], crc)

    def test_live_changes_and_stop(self):
        listener = socket.socket()
        listener.bind(('127.0.0.1', 0))
        listener.listen()
        listener.settimeout(0.1)
        ended = threading.Event()
        identities = set()
        lock = threading.Lock()
        handlers = []

        def serve(connection):
            with connection:
                connection.settimeout(2)
                try:
                    self.assertEqual(exact(connection, 2), b'\x00\x0f')
                    identity = exact(connection, 15)
                    with lock:
                        identities.add(identity)
                    connection.sendall(b'\x01')
                    while not ended.is_set():
                        header = exact(connection, 8)
                        exact(connection, struct.unpack('>I', header[4:])[0] + 4)
                        connection.sendall(b'\x00\x00\x00\x01')
                except (OSError, RuntimeError):
                    pass

        def accept():
            while not ended.is_set():
                try:
                    connection, _ = listener.accept()
                except socket.timeout:
                    continue
                thread = threading.Thread(target=serve, args=(connection,))
                handlers.append(thread)
                thread.start()

        def until(predicate):
            deadline = time.monotonic() + 5
            while not predicate() and time.monotonic() < deadline:
                time.sleep(0.02)
            self.assertTrue(predicate())

        server = threading.Thread(target=accept)
        server.start()
        simulator = Simulator()
        try:
            simulator.start(Settings(port=listener.getsockname()[1], devices=1, interval=2))
            until(lambda: simulator.snapshot()[1] >= 1)
            simulator.apply(3, 0.1)
            until(lambda: simulator.snapshot()[0] == 3 and simulator.snapshot()[1] >= 8)
            with lock:
                self.assertEqual(len(identities), 3)
            simulator.apply(1, 0.2)
            until(lambda: simulator.snapshot()[0] == 1)
            simulator.stop()
            until(lambda: not simulator.running)
            self.assertEqual(simulator.snapshot()[0], 0)
            simulator.start(Settings(port=listener.getsockname()[1], devices=1, interval=0.1))
            until(lambda: simulator.snapshot()[1] > 0)
        finally:
            simulator.stop()
            if simulator._controller:
                simulator._controller.join(5)
            ended.set()
            server.join(2)
            listener.close()
            for handler in handlers:
                handler.join(3)


if __name__ == '__main__':
    unittest.main()
