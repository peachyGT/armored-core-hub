"""Run an installed Universal Modder with its tool cache confined to a QA lab.

UM_PLUGIN_ROOT can select another installed version; no plugin source is modified.
Pass --lab before the normal um arguments. This wrapper never publishes notes.
"""
import argparse
import os
import sys
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--lab', required=True)
args, commands = parser.parse_known_args()
lab = Path(args.lab).resolve()
lab.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(lab / 'tools/python-libs'))
plugin = Path(os.environ.get('UM_PLUGIN_ROOT', str(Path.home() / '.codex/plugins/cache/universal-modder/universal-modder/0.2.0')))
if not (plugin / 'um/cli.py').is_file():
    parser.error('Install Universal Modder or set UM_PLUGIN_ROOT to its installed directory.')
sys.dont_write_bytecode = True
sys.path.insert(0, str(plugin))
from um import win
tool_cache = lab / 'tools/universal-modder'
tool_cache.mkdir(parents=True, exist_ok=True)
win.local_appdata = lambda: tool_cache
from um.cli import main
main(commands)
