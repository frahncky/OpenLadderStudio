"""Portable safety checks for the Windows metadata-only probe; no COM access."""
from pathlib import Path
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'scripts' / 'windows' / 'Tp02DriverProbe.ps1'


class DriverProbeStaticTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.script = SCRIPT.read_text(encoding='utf-8')

    def test_uses_windows7_wmi_classes(self):
        for item in ('Win32_PnPEntity', 'Win32_PnPSignedDriver', 'Win32_SerialPort'):
            self.assertIn(item, self.script)
        self.assertNotIn('Get-CimInstance', self.script)

    def test_no_serial_io_or_mutations(self):
        for pattern in (r'New-Object\s+System\.IO\.Ports', r'\bSerialPort\s*\(',
                        r'\.Open\s*\(', r'\.Write\s*\(', r'\.DtrEnable\s*=',
                        r'\.RtsEnable\s*=', r'Set-WmiInstance', r'Set-ItemProperty'):
            self.assertNotRegex(self.script, pattern)

    def test_identifiers_only_used_for_driver_matching(self):
        self.assertIn('$candidate.DeviceID, $pnpId', self.script)
        self.assertNotRegex(self.script, r"\$lines\.Add\([^\n]*(?:\$pnpId|PNPDeviceID|\$candidate\.DeviceID)")

    def test_port_validation_and_empty_device_handling(self):
        self.assertIn('COM1', self.script)
        self.assertIn('COM1..COM256', self.script)
        self.assertIn('DispositivosEncontrados=', self.script)


if __name__ == '__main__':
    unittest.main()
