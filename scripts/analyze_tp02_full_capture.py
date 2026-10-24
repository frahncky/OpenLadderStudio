#!/usr/bin/env python3
"""Offline, read-only audit of a TP02 Full Protocol Capture ZIP or folder.

Never opens a serial port, transmits commands, or publishes PLC memory contents.
"""
import argparse
import csv
import io
import json
import re
import sys
import zipfile
from collections import Counter
from pathlib import Path

HELLO = bytes.fromhex('43 4F 4E 2D 49 43 42 0D')
F0 = bytes.fromhex('F0 00 0F')
F0_REPLY = bytes.fromhex('00 02 10 22 CB')
PG38 = bytes.fromhex('38 00 C7')
SESSION = re.compile(r'^V\d+-S(\d+)-(HELLO-\d+|F0-ONLY)$')


def load_csv(path):
    """Read a capture without extracting arbitrary ZIP entries to disk."""
    path = Path(path)
    if path.is_dir():
        text = (path / 'responses.csv').read_text(encoding='utf-8-sig')
    else:
        with zipfile.ZipFile(path) as archive:
            matches = [n for n in archive.namelist() if n.endswith('/responses.csv') or n == 'responses.csv']
            if len(matches) != 1:
                raise ValueError('Expected exactly one responses.csv in ZIP')
            text = archive.read(matches[0]).decode('utf-8-sig')
    reader = csv.DictReader(io.StringIO(text))
    expected = {'sequence', 'label', 'elapsed_ms', 'tx', 'rx', 'context'}
    if not reader.fieldnames or not expected.issubset(reader.fieldnames):
        raise ValueError('Missing capture CSV columns')
    return list(reader)


def audit(path):
    rows = load_csv(path)
    if not rows:
        raise ValueError('Capture has no transactions')
    sessions = {}
    counters = Counter()
    issues = []
    pages = {}
    observed_seq = set()
    qualified_seq = None
    last_qualified = None
    first_end = None
    total_words_read = 0

    for row in rows:
        try:
            seq = int(row['sequence'])
            tx = bytes.fromhex(row['tx'])
            rx = bytes.fromhex(row['rx'])
            elapsed = int(row['elapsed_ms'])
        except (ValueError, TypeError) as exc:
            raise ValueError('Invalid sequence/hex/elapsed in CSV row') from exc
        if seq in observed_seq or seq < 1 or elapsed < 0:
            issues.append(f'Invalid/duplicate sequence or duration at row {seq}')
        observed_seq.add(seq)
        match = SESSION.match(row['label'])
        session = int(match.group(1)) if match else None
        if session is not None:
            sessions.setdefault(session, {'hello_attempts': 0, 'hello_ok': False,
                                          'f0_attempts': 0, 'f0_ok': False})
        if tx == HELLO:
            counters['hello_attempts'] += 1
            if session is not None:
                sessions[session]['hello_attempts'] += 1
            if rx in (bytes.fromhex('80 01 09 75'), bytes.fromhex('C0 01 09 35')):
                counters['hello_ok'] += 1
                if session is not None:
                    sessions[session]['hello_ok'] = True
            elif rx:
                issues.append(f'Unexpected HELLO response at sequence {seq}')
        elif tx == F0:
            counters['f0_attempts'] += 1
            if session is not None:
                sessions[session]['f0_attempts'] += 1
            if rx == F0_REPLY:
                counters['f0_ok'] += 1
                qualified_seq = seq
                last_qualified = session
                if session is not None:
                    sessions[session]['f0_ok'] = True
                    if not sessions[session]['hello_ok']:
                        issues.append(f'F0 succeeded without HELLO in session {session}')
            elif rx:
                issues.append(f'Unexpected F0 response at sequence {seq}')
        elif tx == PG38 or (tx and tx[0] in (0x34, 0x0A)):
            counters['read_attempts'] += 1
            if not rx:
                counters['read_no_response'] += 1
            if qualified_seq is None:
                issues.append(f'Read sent before successful F0 at sequence {seq}')
            if tx == PG38:
                counters['pg38_attempts'] += 1
            elif tx[0] == 0x34:
                counters['pg34_attempts'] += 1
                if len(tx) != 6 or (sum(tx) & 255) != 255 or len(rx) != 243 or rx[:2] != b'\x00\xf0':
                    issues.append(f'Invalid PG34 request/response geometry at sequence {seq}')
                elif (sum(rx) & 255) == 255:
                    start = (tx[2] << 8) | tx[3]
                    if start in pages:
                        issues.append(f'Duplicate PG34 page at {start}')
                    pages[start] = rx
            else:
                counters['memory_read_attempts'] += 1
        else:
            issues.append(f'Non-SAFE or unrecognized TX at sequence {seq}: opcode {tx[0]:02X}' if tx else
                          f'Empty TX at sequence {seq}')
        if rx:
            counters['responses'] += 1
            if len(rx) < 3 or len(rx) != rx[1] + 3 or (sum(rx) & 255) != 255:
                counters['invalid_frames'] += 1
                issues.append(f'Invalid response length/checksum at sequence {seq}')
    if len(observed_seq) != len(rows) or observed_seq != set(range(1, len(rows) + 1)):
        issues.append('Non-contiguous transaction sequence')
    if counters['f0_ok'] == 0:
        issues.append('No qualified HELLO/F0 session')
    if not pages:
        issues.append('No valid PG34 program pages')
    if pages:
        for start in sorted(pages):
            if first_end is not None:
                break
            if start != total_words_read:
                issues.append(f'PG34 page gap: expected {total_words_read}, received {start}')
                break
            raw = pages[start]
            for i in range(80):
                total_words_read += 1
                if raw[2 + 2 * i: 4 + 2 * i] == b'\x00\x70':
                    first_end = start + i
                    break
        if first_end is None:
            issues.append('No END found in complete PG34 pages')
    if counters['read_no_response']:
        issues.append(f"{counters['read_no_response']} reads without reply")

    return {
        'result': 'PASS' if not issues else 'CHECK',
        'transactions': len(rows),
        'sessions_opened': max(sessions, default=0),
        'hello_attempts': counters['hello_attempts'],
        'hello_replies': counters['hello_ok'],
        'f0_attempts': counters['f0_attempts'],
        'f0_replies': counters['f0_ok'],
        'qualified_session': last_qualified,
        'reads_after_qualification': sum(
            1 for r in rows if qualified_seq is not None and int(r['sequence']) > qualified_seq
            and r['context'] in ('program-read', 'memory-read')),
        'read_attempts': counters['read_attempts'],
        'read_without_reply': counters['read_no_response'],
        'pg34_pages': sorted(pages),
        'end_step': first_end,
        'program_words_through_end': first_end + 1 if first_end is not None else None,
        'responses': counters['responses'],
        'invalid_response_frames': counters['invalid_frames'],
        'sessions': [{'number': num, **sessions[num]} for num in sorted(sessions)],
        'issues': issues,
        'note': 'Offline audit only; no PLC write or physical link diagnosis.'
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', help='ZIP or extracted folder containing responses.csv')
    parser.add_argument('--compare', help='Optional second capture ZIP or folder')
    parser.add_argument('--json', action='store_true', help='Print compact metadata, never raw memory')
    args = parser.parse_args(argv)
    try:
        result = audit(args.capture)
        if args.compare:
            result = {'baseline': result, 'comparison': audit(args.compare)}
    except (OSError, ValueError, zipfile.BadZipFile, KeyError) as exc:
        print(f'Capture audit failed: {exc}', file=sys.stderr)
        return 2
    print(json.dumps(result, ensure_ascii=False, indent=2))
    values = [result['baseline'], result['comparison']] if args.compare else [result]
    return 0 if all(v['result'] == 'PASS' for v in values) else 1


if __name__ == '__main__':
    sys.exit(main())
