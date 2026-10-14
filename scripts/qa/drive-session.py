"""Drive/capture only the emulator owned by a currently running Hub QA session.

Uses the installed Universal Modder; refuses ambiguous process names, stale sessions,
or executables outside the lab. Input never retries by stealing foreground focus.
"""
import argparse
import json
import os
import re
import sys
import time
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--lab', required=True)
p.add_argument('--shot')
p.add_argument('--record', help='Evidence basename for owned-window video and process-only audio')
p.add_argument('commands', nargs='*')
a = p.parse_args()
lab = Path(a.lab).resolve()
session_path = lab / 'hub/logs/qa-session.json'
session = json.loads(session_path.read_text(encoding='utf-8-sig'))
if not session.get('running'):
    p.error('No active Hub QA session.')
expected = Path(session['executable']).resolve()
if not expected.is_relative_to(lab / 'hub/runtime/sessions'):
    p.error('Session executable is outside the dedicated QA runtime.')
pid = int(session['processId'])
sys.dont_write_bytecode = True
sys.path.insert(0, str(lab / 'tools/python-libs'))
sys.path.insert(0, os.environ.get('UM_PLUGIN_ROOT', str(Path.home() / '.codex/plugins/cache/universal-modder/universal-modder/0.2.0')))
from um import win
win.local_appdata = lambda: lab / 'tools/universal-modder'
# Upstream 0.2.0 ignores SendInput's return value. A failed injection must not be
# mistaken for a successful control test. Keep this diagnostic copy in the lab.
original_tool_path = win.tool_path
def checked_tool_path(name):
    original = Path(original_tool_path(name))
    if name != 'WinDrive.ps1':
        return str(original)
    source = original.read_text(encoding='utf-8-sig')
    source = source.replace('[DllImport("user32.dll")] static extern uint SendInput', '[DllImport("user32.dll", SetLastError=true)] static extern uint SendInput')
    source = source.replace('SendInput((uint)i.Length, i, Marshal.SizeOf(typeof(INPUT)));', 'if(SendInput((uint)i.Length, i, Marshal.SizeOf(typeof(INPUT))) != i.Length) throw new InvalidOperationException("SendInput failed: " + Marshal.GetLastWin32Error());')
    checked = original.with_name('WinDrive-checked.ps1')
    checked.write_text(source, encoding='utf-8-sig')
    return str(checked)
win.tool_path = checked_tool_path

def owned():
    text = win.powershell(f"Get-Process -Id {pid} -ErrorAction Stop | Select-Object Id,Path,ProcessName,MainWindowTitle,@{{n='Hwnd';e={{[int64]$_.MainWindowHandle}}}} | ConvertTo-Json -Compress")
    proc = json.loads(text)
    if Path(proc['Path']).resolve() != expected or not proc['Hwnd']:
        raise RuntimeError('Owned process is absent or has no window.')
    if not re.fullmatch(r'[A-Za-z0-9_.-]+',proc['ProcessName']):
        raise RuntimeError('Unexpected executable name.')
    # Avoid unrelated window titles (and upstream's locale-dependent decoding).
    peers = [x for x in win.processes(proc['ProcessName']) if x['ProcessName'].lower() == proc['ProcessName'].lower()]
    if len(peers) != 1 or peers[0]['Id'] != pid:
        raise RuntimeError('Ambiguous process name; refusing to send input.')
    return proc

proc = owned()
print(json.dumps(proc))
driver = win.Drive(proc['ProcessName']) if a.commands else None
held = set()
recorder = None

def background_key(key, duration=150):
    """Post only to the owned game window; never activate it or inject global input.

    Some Qt emulators accept this while other apps are in use. Failure to observe
    a response is not evidence of a broken physical/foreground keyboard mapping.
    """
    if not re.fullmatch(r'0x[0-9A-Fa-f]{2}', key):
        raise ValueError('Use a two-digit virtual-key code.')
    proc = owned()
    duration = min(2000, max(20, int(duration)))
    code = '''
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class QaWindowKey {
 [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint key, uint type);
 [DllImport("user32.dll", SetLastError=true)] public static extern bool PostMessage(IntPtr window, uint message, IntPtr key, IntPtr data);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
 public static void Send(long handle, uint expectedPid, int key, bool up) {
  uint pid; GetWindowThreadProcessId((IntPtr)handle, out pid);
  if(pid!=expectedPid) throw new InvalidOperationException("Owned window changed");
  uint flags=1 | (MapVirtualKey((uint)key, 0)<<16);
  if(key>=0x21 && key<=0x2e) flags |= 1u<<24;
  if(up) flags |= 0xc0000000;
  if(!PostMessage((IntPtr)handle, up?0x101u:0x100u, (IntPtr)key, (IntPtr)(long)flags)) throw new InvalidOperationException("PostMessage failed");
 }
}
'@
'''
    code += f'try {{ [QaWindowKey]::Send({proc["Hwnd"]}, {pid}, {key}, $false); Start-Sleep -Milliseconds {duration} }} finally {{ [QaWindowKey]::Send({proc["Hwnd"]}, {pid}, {key}, $true) }}'
    win.powershell(code)

def capture(name):
    proc = owned()
    destination = (lab / 'evidence' / session['edition'] / name).resolve()
    if not destination.is_relative_to(lab / 'evidence') or destination.suffix.lower() != '.png':
        p.error('Screenshot must be a PNG within the lab evidence directory.')
    win.shot(str(destination), hwnd=proc['Hwnd'], scale=0.6)
try:
    if a.record:
        target=(lab / 'evidence' / session['edition'] / a.record).resolve()
        if not target.is_relative_to(lab / 'evidence') or target.suffix:
            p.error('Recording needs a basename within the lab evidence directory.')
        target.parent.mkdir(parents=True,exist_ok=True)
        proc=owned()
        recorder=win.Recorder(hwnd=proc['Hwnd'],pid=pid,out=str(target),max_width=1280,fps=30)
        recorder.start()
    if driver:
        print('idle seconds:', driver.cmd('idle', retry=False))
    for command in a.commands:
        owned()
        if command.startswith('wait '):
            time.sleep(min(10, max(0, float(command.split()[1]))))
        elif command.startswith('shot '):
            capture(command[5:])
        elif command.startswith('background-key '):
            parts = command.split()
            background_key(parts[1], parts[2] if len(parts)>2 else 150)
            print(command, '=> posted to owned window (response requires visual verification)')
        else:
            print(command, '=>', driver.cmd(command, retry=False))
            parts = command.split()
            if len(parts) == 3 and parts[0] == 'key':
                if parts[2] == 'down': held.add(parts[1])
                elif parts[2] == 'up': held.discard(parts[1])
            time.sleep(0.15)
    if a.shot:
        capture(a.shot)
finally:
    try:
        if driver:
            for key in held:
                try: driver.cmd(f'key {key} up', retry=False)
                except RuntimeError: pass
            driver.close()
    finally:
        if recorder:
            recorder.stop()
