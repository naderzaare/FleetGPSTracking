"""Run on Windows: py -3 tools/GpsSimulator/gui.py. No pip dependencies."""
import queue
import time
import tkinter as tk
from tkinter import messagebox, ttk

from engine import Settings, Simulator


class App:
    def __init__(self, root):
        self.root = root
        self.simulator = Simulator()
        self.closing = False
        root.title('GPS Simulator | شبیه‌ساز دستگاه GPS')
        root.minsize(640, 480)
        panel = ttk.Frame(root, padding=20)
        panel.pack(fill='both', expand=True)
        panel.columnconfigure(1, weight=1)
        ttk.Label(panel, text='Teltonika GPS Simulator', font=('Segoe UI', 18, 'bold')).grid(row=0, column=0, columnspan=2, sticky='w', pady=(0, 16))
        self.host = tk.StringVar(value='127.0.0.1')
        self.port = tk.StringVar(value='5027')
        self.devices = tk.StringVar(value='1')
        self.interval = tk.StringVar(value='5')
        self.host_entry = self.field(panel, 1, 'آدرس سرور / Server', self.host)
        self.port_entry = self.field(panel, 2, 'پورت TCP / Port', self.port)
        self.field(panel, 3, 'تعداد دستگاه / Devices', self.devices, (1, 100, 1))
        self.field(panel, 4, 'فاصله ارسال هر دستگاه (ثانیه)', self.interval, (0.1, 3600, 0.1))
        buttons = ttk.Frame(panel)
        buttons.grid(row=5, column=0, columnspan=2, sticky='ew', pady=16)
        self.start_button = ttk.Button(buttons, text='شروع / Start', command=self.start)
        self.start_button.pack(side='left', padx=(0, 8))
        self.apply_button = ttk.Button(buttons, text='اعمال تغییرات / Apply', command=self.apply, state='disabled')
        self.apply_button.pack(side='left', padx=(0, 8))
        self.stop_button = ttk.Button(buttons, text='توقف / Stop', command=self.stop, state='disabled')
        self.stop_button.pack(side='left')
        self.status = tk.StringVar(value='Stopped')
        self.counters = tk.StringVar(value='Connected: 0 | ACK records: 0 | Errors: 0')
        ttk.Label(panel, textvariable=self.status).grid(row=6, column=0, columnspan=2, sticky='w')
        ttk.Label(panel, textvariable=self.counters).grid(row=7, column=0, columnspan=2, sticky='w', pady=6)
        ttk.Label(panel, text='داده آزمایشی است؛ موقعیت GPS معتبر ندارد.\nApply تعداد و فاصله ارسال را حین اجرا تغییر می‌دهد. تغییر سرور نیاز به توقف دارد.', justify='right').grid(row=8, column=0, columnspan=2, sticky='e', pady=8)
        self.log = tk.Text(panel, height=10, wrap='word', state='disabled', font=('Consolas', 10))
        self.log.grid(row=9, column=0, columnspan=2, sticky='nsew')
        panel.rowconfigure(9, weight=1)
        root.protocol('WM_DELETE_WINDOW', self.close)
        root.after(100, self.poll)

    def field(self, panel, row, label, variable, limits=None):
        ttk.Label(panel, text=label).grid(row=row, column=0, sticky='e', padx=(0, 15), pady=6)
        widget = ttk.Entry(panel, textvariable=variable) if limits is None else ttk.Spinbox(
            panel, textvariable=variable, from_=limits[0], to=limits[1], increment=limits[2])
        widget.grid(row=row, column=1, sticky='ew', pady=6)
        return widget

    def settings(self):
        return Settings(self.host.get().strip(), int(self.port.get()), int(self.devices.get()), float(self.interval.get()))

    def start(self):
        try:
            self.simulator.start(self.settings())
        except ValueError as error:
            messagebox.showerror('Invalid settings', str(error))
            return
        self.host_entry.configure(state='disabled')
        self.port_entry.configure(state='disabled')
        self.start_button.configure(state='disabled')
        self.apply_button.configure(state='normal')
        self.stop_button.configure(state='normal')
        self.status.set('Running — synthetic invalid-GPS data')

    def apply(self):
        try:
            self.simulator.apply(int(self.devices.get()), float(self.interval.get()))
            self.status.set('Updated — device count and interval applied')
        except ValueError as error:
            messagebox.showerror('Invalid settings', str(error))

    def stop(self):
        self.simulator.stop()
        self.stop_button.configure(state='disabled')
        self.apply_button.configure(state='disabled')
        self.status.set('Stopping…')

    def close(self):
        self.closing = True
        self.stop()

    def poll(self):
        if not self.simulator.running:
            if self.closing:
                self.root.destroy()
                return
            self.host_entry.configure(state='normal')
            self.port_entry.configure(state='normal')
            self.start_button.configure(state='normal')
            self.apply_button.configure(state='disabled')
            self.stop_button.configure(state='disabled')
            self.status.set('Stopped')
        connected, accepted, errors = self.simulator.snapshot()
        self.counters.set(f'Connected: {connected} | ACK records: {accepted} | Errors: {errors}')
        while True:
            try:
                message = self.simulator.events.get_nowait()
            except queue.Empty:
                break
            self.log.configure(state='normal')
            self.log.insert('end', time.strftime('%H:%M:%S ') + message + '\n')
            if int(self.log.index('end-1c').split('.')[0]) > 200:
                self.log.delete('1.0', '2.0')
            self.log.see('end')
            self.log.configure(state='disabled')
        self.root.after(100, self.poll)


if __name__ == '__main__':
    window = tk.Tk()
    App(window)
    window.mainloop()
