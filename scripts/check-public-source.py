"""Check Git's source list for private files and obvious credential patterns."""
import json
import pathlib
import re
import subprocess
import sys

root = pathlib.Path(__file__).resolve().parents[1]
names = subprocess.check_output(
    ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=root
).decode("utf-8").split("\0")
private = ("work/", "outputs/", "runtime/", "games/", "saves/", "firmware/", "cache/", "config/", ".kiro/", "docs/research/", "docs/testing/")
allowed_binary = {
    "src/Hub.App/Assets/Hub.ico", "src/Hub.App/Assets/Cinzel.ttf",
    "src/Hub.App/Assets/BarlowSemiCondensed-SemiBold.ttf", "third-party/7zip/7zr.exe",
    "tests/Hub.Tests/fixtures/password-media.7z", "tests/Hub.Tests/fixtures/synthetic-media.7z",
}
patterns = {
    "GitHub token": r"(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})",
    "private key": r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----",
    "private user path": r"[A-Z]:[/\\]+Users[/\\]+JackF(?:[/\\]|\b)",
    "removed game-download site": r"vimm\.net",
}
findings = []
checked = 0
total_bytes = 0
for name in sorted(set(names)):
    if not name or name.startswith((".agents/", ".impeccable/", "design/")):
        continue
    file = root / name
    if not file.is_file():
        continue
    checked += 1
    total_bytes += file.stat().st_size
    if name.startswith(private) or "/bin/" in name or "/obj/" in name:
        findings.append({"file": name, "reason": "private/generated path"})
    if file.stat().st_size > 15 * 1024 * 1024:
        findings.append({"file": name, "reason": "unexpected large source file"})
    try:
        text = file.read_text(encoding="utf-8-sig")
    except UnicodeError:
        if name not in allowed_binary and not (name.startswith("docs/media/") and file.suffix.lower() in {".png", ".gif", ".jpg"}):
            findings.append({"file": name, "reason": "unreviewed binary"})
        continue
    for label, pattern in patterns.items():
        # This check's own pattern declarations are examples, not credentials.
        if name == "scripts/check-public-source.py":
            continue
        if re.search(pattern, text, re.IGNORECASE):
            findings.append({"file": name, "reason": label})
print(json.dumps({"checkedFiles": checked, "bytes": total_bytes, "findings": findings}, indent=2))
sys.exit(1 if findings else 0)
