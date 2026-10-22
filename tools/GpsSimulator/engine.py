"""Threaded TCP load simulator. Synthetic identities and invalid-GPS records only."""
from dataclasses import dataclass
import queue
import socket
import struct
import threading
import time

from simulate import PACKET, exact


@dataclass(frozen=True)
class Settings:
    host: str = '127.0.0.1'
    port: int = 5027
    devices: int = 1
    interval: float = 5.0

    def validate(self):
        if not self.host.strip():
            raise ValueError('Enter a server address.')
        if not 1 <= self.port <= 65535:
            raise ValueError('Port must be between 1 and 65535.')
        if not 1 <= self.devices <= 100:
            raise ValueError('Device count must be between 1 and 100.')
        if not 0.1 <= self.interval <= 3600:
            raise ValueError('Interval must be between 0.1 and 3600 seconds.')


def make_packet():
    """Fresh UTC timestamp, known official record structure, recalculated IBM CRC."""
    packet = bytearray(PACKET)
    struct.pack_into('>Q', packet, 10, time.time_ns() // 1_000_000)
    crc = 0
    for value in packet[8:-4]:
        crc ^= value
        for _ in range(8):
            crc = (crc >> 1) ^ (0xA001 if crc & 1 else 0)
    struct.pack_into('>I', packet, len(packet) - 4, crc)
    return bytes(packet)


class Simulator:
    def __init__(self):
        self.events = queue.Queue(maxsize=200)
        self._lock = threading.Lock()
        self._settings = Settings()
        self._stop = threading.Event()
        self._controller = None
        self._workers = {}
        self._sockets = set()
        self._connected = 0
        self._accepted = 0
        self._errors = 0

    @property
    def running(self):
        return self._controller is not None and self._controller.is_alive()

    def snapshot(self):
        with self._lock:
            return self._connected, self._accepted, self._errors

    def start(self, settings):
        settings.validate()
        if self.running:
            raise ValueError('Stop the previous run first.')
        with self._lock:
            self._settings = settings
            self._accepted = self._errors = 0
        self._stop.clear()
        self._controller = threading.Thread(target=self._manage, daemon=True)
        self._controller.start()

    def apply(self, devices, interval):
        with self._lock:
            updated = Settings(self._settings.host, self._settings.port, devices, interval)
            updated.validate()
            self._settings = updated

    def stop(self):
        self._stop.set()
        with self._lock:
            sockets = list(self._sockets)
        for connection in sockets:
            try:
                connection.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass

    def _message(self, text):
        try:
            self.events.put_nowait(text)
        except queue.Full:
            pass

    def _manage(self):
        try:
            while not self._stop.is_set():
                with self._lock:
                    count = self._settings.devices
                for index, (thread, retired) in list(self._workers.items()):
                    if index >= count:
                        retired.set()
                    if not thread.is_alive():
                        del self._workers[index]
                for index in range(count):
                    if index not in self._workers:
                        retired = threading.Event()
                        thread = threading.Thread(target=self._device, args=(index, retired), daemon=True)
                        self._workers[index] = thread, retired
                        thread.start()
                self._stop.wait(0.1)
        finally:
            self._stop.set()
            for thread, retired in self._workers.values():
                retired.set()
            for thread, _ in self._workers.values():
                thread.join()
            self._workers.clear()
            self._message('Stopped. All device connections are closed.')

    def _device(self, index, retired):
        def stopped():
            return self._stop.is_set() or retired.is_set()

        def wait(seconds):
            end = time.monotonic() + seconds
            while not stopped() and time.monotonic() < end:
                self._stop.wait(min(0.1, max(0, end - time.monotonic())))

        while not stopped():
            connection = None
            connected = False
            try:
                with self._lock:
                    settings = self._settings
                # Fixed-width synthetic identity, stable for each simulated device.
                identity = f'{index:015d}'.encode('ascii')
                connection = socket.create_connection((settings.host, settings.port), timeout=3)
                with self._lock:
                    self._sockets.add(connection)
                if stopped():
                    break
                connection.sendall(b'\x00\x0f' + identity)
                if exact(connection, 1) != b'\x01':
                    raise RuntimeError('Identity rejected; check receiver allowlist.')
                with self._lock:
                    self._connected += 1
                connected = True
                last_sent = None
                while not stopped():
                    with self._lock:
                        interval = self._settings.interval
                    if last_sent is not None and time.monotonic() - last_sent < interval:
                        wait(min(0.1, interval - (time.monotonic() - last_sent)))
                        continue
                    connection.sendall(make_packet())
                    if exact(connection, 4) != struct.pack('>I', 1):
                        raise RuntimeError('Unexpected record ACK.')
                    with self._lock:
                        self._accepted += 1
                    last_sent = time.monotonic()
            except (OSError, RuntimeError) as error:
                if not stopped():
                    with self._lock:
                        self._errors += 1
                    self._message(f'Device {index + 1}: {error}')
                    wait(2)
            finally:
                if connection is not None:
                    with self._lock:
                        self._sockets.discard(connection)
                    connection.close()
                if connected:
                    with self._lock:
                        self._connected -= 1
