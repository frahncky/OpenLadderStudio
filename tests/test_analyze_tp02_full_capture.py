"""Synthetic tests; no real PLC data and no serial access."""
import csv
import io
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from analyze_tp02_full_capture import audit


def checksum_frame(body):
    return bytes(body) + bytes([(255 - sum(body)) & 255])


def fixture_zip(path, bad_checksum=False, unsafe_tx=False, missing_f0=False):
    program = bytearray([0, 16] * 80)
    program[4:6] = b'\x00\x70'
    page = checksum_frame(b'\x00\xf0' + program + bytes(80))
    if bad_checksum:
        page = page[:-1] + bytes([page[-1] ^ 1])
    hello = bytes.fromhex('43 4f 4e 2d 49 43 42 0d')
    requests = [
        ('V160-S01-HELLO-1', hello, b'', 'session'),
        ('V160-S01-HELLO-2', hello, bytes.fromhex('80 01 09 75'), 'session'),
        ('V160-S01-F0-ONLY', bytes.fromhex('f0 00 0f'), b'', 'qualification'),
        ('V160-S02-HELLO-1', hello, bytes.fromhex('80 01 09 75'), 'session'),
        ('V160-S02-F0-ONLY', bytes.fromhex('f0 00 0f'),
         b'' if missing_f0 else bytes.fromhex('00 02 10 22 cb'), 'qualification'),
        ('00-baseline-program-38-TRY1', bytes.fromhex('38 00 c7'),
         bytes.fromhex('00 02 02 84 77'), 'program-read'),
        ('00-baseline-program-34-0000-TRY1', bytes.fromhex('34 03 00 00 a0 28'),
         page, 'program-read'),
        ('00-baseline-memory-0A-C-01', bytes.fromhex('0a 03 53 f9 06 a0'),
         checksum_frame(b'\x00\x02\x00\x00'), 'memory-read'),
    ]
    if unsafe_tx:
        requests.append(('unsafe', bytes.fromhex('33 00 cc'), b'', 'program-write'))
    buffer = io.StringIO()
    writer = csv.writer(buffer)
    writer.writerow(['sequence', 'label', 'elapsed_ms', 'tx', 'rx', 'parsed_frames', 'context'])
    for i, (label, tx, rx, context) in enumerate(requests, 1):
        writer.writerow([i, label, 250, tx.hex(' '), rx.hex(' '), '', context])
    with zipfile.ZipFile(path, 'w') as archive:
        archive.writestr('capture/responses.csv', buffer.getvalue())


class CaptureAuditTests(unittest.TestCase):
    def test_qualified_readback(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'safe.zip'
            fixture_zip(path)
            result = audit(path)
            self.assertEqual('PASS', result['result'])
            self.assertEqual(2, result['sessions_opened'])
            self.assertEqual(3, result['hello_attempts'])
            self.assertEqual(2, result['hello_replies'])
            self.assertEqual(1, result['f0_replies'])
            self.assertEqual(3, result['reads_after_qualification'])
            self.assertEqual([0], result['pg34_pages'])
            self.assertEqual(2, result['end_step'])
            self.assertEqual(3, result['program_words_through_end'])

    def test_bad_checksum_is_reported(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'bad.zip'
            fixture_zip(path, bad_checksum=True)
            result = audit(path)
            self.assertEqual('CHECK', result['result'])
            self.assertEqual(1, result['invalid_response_frames'])

    def test_unsafe_tx_is_reported(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'unsafe.zip'
            fixture_zip(path, unsafe_tx=True)
            self.assertEqual('CHECK', audit(path)['result'])
            self.assertTrue(any('Non-SAFE' in x for x in audit(path)['issues']))

    def test_missing_f0_blocks_reads(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'unqualified.zip'
            fixture_zip(path, missing_f0=True)
            result = audit(path)
            self.assertEqual('CHECK', result['result'])
            self.assertTrue(any('before successful F0' in x for x in result['issues']))

    def test_rejects_zip_without_csv(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'empty.zip'
            with zipfile.ZipFile(path, 'w') as archive:
                archive.writestr('unrelated.txt', 'nothing')
            with self.assertRaisesRegex(ValueError, 'responses.csv'):
                audit(path)


if __name__ == '__main__':
    unittest.main()
