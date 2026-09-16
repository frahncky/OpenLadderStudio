"""Synthetic, isolated tests of TP02 handshake timing; no COM or PLC."""
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from diagnose_tp02_link import compare, diagnose, log_timing, main
from test_analyze_tp02_full_capture import fixture_zip


class LinkTimingTests(unittest.TestCase):
    def test_both_captures_pass_and_pages_match(self):
        with tempfile.TemporaryDirectory() as tmp:
            a, b = Path(tmp) / 'first.zip', Path(tmp) / 'second.zip'
            fixture_zip(a)
            fixture_zip(b)
            r = compare(a, b)
            self.assertTrue(r['difference']['both_audits_pass'])
            self.assertTrue(r['difference']['pg34_pages_identical'])
            self.assertEqual(0, r['difference']['hello_attempts_delta'])
            self.assertEqual(250, r['first']['latency']['hello_silent']['median_ms'])
            self.assertNotIn('00 70', str(r))
            self.assertNotIn('_pg34', str(r))

    def test_sessions_separated_from_failed_f0(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'trace.zip'
            fixture_zip(p)
            r = diagnose(p)
            self.assertEqual(2, r['qualified_session'])
            self.assertEqual(2, r['sessions'][0]['hello_reply_attempt'])
            self.assertFalse(r['sessions'][0]['f0_replied'])
            self.assertTrue(r['sessions'][1]['f0_replied'])
            self.assertEqual(500, r['silent_handshake_elapsed_ms'])
            self.assertEqual(0, r['latency']['read_silent']['count'])

    def test_log_elapsed_qualify_and_total_and_midnight(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'trace.zip'
            with zipfile.ZipFile(p, 'w') as z:
                z.writestr('capture/session.log',
                           '[23:59:58.000] TP02 FULL PROTOCOL CAPTURE iniciado.\n'
                           '[23:59:59.900] HELLO+F0 confirmados\n'
                           '[00:00:01.000] Campanha concluida.\n')
            self.assertEqual({'time_to_qualification_ms': 1900, 'total_capture_ms': 3000}, log_timing(p))

    def test_no_log_keeps_timing_unknown(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'trace.zip'
            fixture_zip(p)
            self.assertIsNone(diagnose(p)['log_timing'])

    def test_unsafe_frames_cannot_pass(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / 'unsafe.zip'
            fixture_zip(p, unsafe_tx=True)
            self.assertEqual('CHECK', diagnose(p)['audit_result'])
            self.assertEqual(1, main([str(p), '--out', str(Path(tmp) / 'report.json')]))

    def test_missing_capture_fails_without_port_access(self):
        self.assertEqual(2, main(['/does/not/exist.zip']))


if __name__ == '__main__':
    unittest.main()
