"""Read Windows mixer state for the active, owned QA emulator; never change it.

Optional QA dependency: pycaw, installed in the lab's tools/audio-libs directory.
No unrelated process/session details are printed or persisted.
"""
import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--lab', required=True)
parser.add_argument('--out', help='JSON filename beneath this edition evidence directory')
args = parser.parse_args()
lab = Path(args.lab).resolve()
session = json.loads((lab / 'hub/logs/qa-session.json').read_text(encoding='utf-8-sig'))
if not session.get('running'):
    parser.error('No active Hub QA session.')
executable = Path(session['executable']).resolve()
if not executable.is_relative_to(lab / 'hub/runtime/sessions'):
    parser.error('Session executable is outside the dedicated QA runtime.')
sys.dont_write_bytecode = True
sys.path.insert(0, str(lab / 'tools/audio-libs'))
import psutil
from pycaw.pycaw import AudioUtilities

pid = int(session['processId'])
process = psutil.Process(pid)
started = datetime.fromisoformat(session['started']).timestamp()
if Path(process.exe()).resolve() != executable or process.create_time() < started - 5:
    parser.error('The recorded process is not the current QA emulator.')
device = AudioUtilities.GetSpeakers()
endpoint = device.EndpointVolume if device else None
report = {
    'checkedAt': datetime.now(timezone.utc).isoformat(),
    'edition': session['edition'],
    'processId': pid,
    'defaultEndpoint': None if not endpoint else {
        'muted': bool(endpoint.GetMute()),
        'volume': endpoint.GetMasterVolumeLevelScalar(),
    },
    'sessionsOnDefaultEndpoint': [],
    'scope': 'Read-only. An empty session list may mean a non-default output device.',
}
for audio_session in AudioUtilities.GetAllSessions():
    if audio_session.ProcessId == pid:
        volume = audio_session.SimpleAudioVolume
        report['sessionsOnDefaultEndpoint'].append({
            'muted': bool(volume.GetMute()),
            'volume': volume.GetMasterVolume(),
        })
result = json.dumps(report, indent=2)
print(result)
if args.out:
    destination = (lab / 'evidence' / session['edition'] / args.out).resolve()
    if not destination.is_relative_to(lab / 'evidence') or destination.suffix != '.json':
        parser.error('Output must be a JSON file within lab evidence.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(result + '\n', encoding='utf-8')
