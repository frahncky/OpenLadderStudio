#!/usr/bin/env python3
"""Compare TP02 SAFE link acquisition using existing ZIP captures, offline only.

No serial access; output contains aggregate metadata, never PLC memory or raw frames.
"""
import argparse
import json
import re
import statistics
import sys
import zipfile
from pathlib import Path

from analyze_tp02_full_capture import HELLO, F0, F0_REPLY, PG38, SESSION, audit, load_csv

LOG_TIME = re.compile(r'^\[(\d{2}):(\d{2}):(\d{2})\.(\d{3})\]')


def _millis(match):
    h, m, s, ms = (int(part) for part in match.groups())
    if h > 23 or m > 59 or s > 59:
        raise ValueError('Invalid session.log timestamp')
    return ((h * 60 + m) * 60 + s) * 1000 + ms


def log_timing(path):
    """Read only timing markers; do not include log lines in the result."""
    path = Path(path)
    if path.is_dir():
        log = path / 'session.log'
        if not log.is_file():
            return None
        text = log.read_text(encoding='utf-8-sig')
    else:
        with zipfile.ZipFile(path) as archive:
            matches = [name for name in archive.namelist()
                       if name == 'session.log' or name.endswith('/session.log')]
            if not matches:
                return None
            if len(matches) != 1:
                raise ValueError('Expected exactly one session.log in ZIP')
            text = archive.read(matches[0]).decode('utf-8-sig')
    start = qualified = finished = None
    last = None
    days = 0
    for line in text.splitlines():
        ts = LOG_TIME.match(line)
        if not ts:
            continue
        t = _millis(ts)
        if last is not None and t < last:
            # Wrap at midnight, not a slight backwards system clock adjustment.
            if last - t > 12 * 3600 * 1000:
                days += 1
            else:
                raise ValueError('Non-monotonic session.log timestamps')
        last = t
        absolute = t + days * 86400000
        if 'FULL PROTOCOL CAPTURE iniciado.' in line and start is None:
            start = absolute
        if 'HELLO+F0 confirmados' in line and qualified is None:
            qualified = absolute
        if 'Campanha concluida.' in line:
            finished = absolute
    if start is None:
        return None
    return {
        'time_to_qualification_ms': qualified - start if qualified is not None else None,
        'total_capture_ms': finished - start if finished is not None else None,
    }


def duration_summary(values):
    """Integer milliseconds, with no raw wire payload included."""
    if not values:
        return {'count': 0, 'min_ms': None, 'median_ms': None, 'max_ms': None}
    return {
        'count': len(values),
        'min_ms': min(values),
        'median_ms': statistics.median(values),
        'max_ms': max(values),
    }


def diagnose(path):
    audited = audit(path)
    categories = {key: [] for key in (
        'hello_reply', 'hello_silent', 'f0_reply', 'f0_silent', 'read_reply', 'read_silent')}
    sessions = {}
    pg34 = {}
    for row in load_csv(path):
        tx = bytes.fromhex(row['tx'])
        rx = bytes.fromhex(row['rx'])
        ms = int(row['elapsed_ms'])
        match = SESSION.fullmatch(row['label'])
        session = int(match.group(1)) if match else None
        if session is not None:
            state = sessions.setdefault(session, {
                'number': session, 'hello_attempts': 0, 'hello_reply_attempt': None,
                'f0_attempts': 0, 'f0_replied': False})
        if tx == HELLO:
            categories['hello_reply' if rx else 'hello_silent'].append(ms)
            if session is not None:
                state['hello_attempts'] += 1
                if rx:
                    state['hello_reply_attempt'] = state['hello_attempts']
        elif tx == F0:
            categories['f0_reply' if rx else 'f0_silent'].append(ms)
            if session is not None:
                state['f0_attempts'] += 1
                state['f0_replied'] = rx == F0_REPLY
        elif tx == PG38 or (tx and tx[0] in (0x34, 0x0A)):
            categories['read_reply' if rx else 'read_silent'].append(ms)
            if tx and tx[0] == 0x34 and len(tx) >= 4:
                address = tx[2] * 256 + tx[3]
                # Retain payload only in process memory to compare captures, never in JSON.
                pg34[address] = rx
    return {
        'audit_result': audited['result'],
        'audit_issues': audited['issues'],
        'qualified_session': audited['qualified_session'],
        'sessions': [sessions[k] for k in sorted(sessions)],
        'latency': {key: duration_summary(values) for key, values in categories.items()},
        'silent_handshake_elapsed_ms': sum(categories['hello_silent'] + categories['f0_silent']),
        'log_timing': log_timing(path),
        '_pg34': pg34,
    }


def sanitized(report):
    return {key: value for key, value in report.items() if not key.startswith('_')}


def compare(first, second):
    a, b = diagnose(first), diagnose(second)
    pages_a, pages_b = a['_pg34'], b['_pg34']
    return {
        'first': sanitized(a),
        'second': sanitized(b),
        'difference': {
            'qualified_session_delta': (b['qualified_session'] - a['qualified_session'])
            if a['qualified_session'] is not None and b['qualified_session'] is not None else None,
            'hello_attempts_delta': (sum(s['hello_attempts'] for s in b['sessions'])
                                     - sum(s['hello_attempts'] for s in a['sessions'])),
            'time_to_qualification_delta_ms': (
                b['log_timing']['time_to_qualification_ms'] - a['log_timing']['time_to_qualification_ms'])
            if a['log_timing'] and b['log_timing'] and
            a['log_timing']['time_to_qualification_ms'] is not None and
            b['log_timing']['time_to_qualification_ms'] is not None else None,
            'pg34_pages_identical': bool(pages_a) and pages_a == pages_b,
            'both_audits_pass': a['audit_result'] == b['audit_result'] == 'PASS',
        },
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('first', help='First SAFE ZIP or extracted capture folder')
    parser.add_argument('second', nargs='?', help='Second ZIP/folder to compare')
    parser.add_argument('--out', help='Save aggregated JSON to file (no raw program contents)')
    args = parser.parse_args(argv)
    try:
        result = compare(args.first, args.second) if args.second else sanitized(diagnose(args.first))
        contents = json.dumps(result, ensure_ascii=False, indent=2) + '\n'
        if args.out:
            Path(args.out).write_text(contents, encoding='utf-8')
        else:
            print(contents, end='')
        passed = result['difference']['both_audits_pass'] if args.second else result['audit_result'] == 'PASS'
        return 0 if passed else 1
    except (OSError, ValueError, UnicodeError, zipfile.BadZipFile, KeyError) as exc:
        print(f'Link diagnosis failed: {exc}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
