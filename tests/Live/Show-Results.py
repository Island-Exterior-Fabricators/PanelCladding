"""Prints a live suite's results file (<evidenceDir>/<suite>.json) as one line per check.

    python tests/Live/Show-Results.py <results.json> [--commands] [--width N]
"""
import json
import sys

path = sys.argv[1]
width = int(sys.argv[sys.argv.index("--width") + 1]) if "--width" in sys.argv else 600
with open(path, encoding="utf-8") as handle:
    data = json.load(handle)
print(f"{data['suite']}: {data['status']}  passed {data['passed']}  failed {data['failed']}  {data['error'][:300]}")
for check in data["checks"]:
    evidence = json.dumps(check["evidence"], ensure_ascii=False)
    print(("PASS " if check["pass"] else "FAIL ") + check["id"] + "  " + evidence[:width])
for key, value in data["notes"].items():
    print("NOTE " + key + ": " + json.dumps(value, ensure_ascii=False)[:width])
if "--commands" in sys.argv:
    for command in data["commands"]:
        print(f">> [{command['step']}] {command['macro']}  ({command['seconds']} s)")
        print("   " + command["history"].strip().replace("\n", "\n   ")[:width * 2])
