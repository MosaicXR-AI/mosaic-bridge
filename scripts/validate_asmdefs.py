#!/usr/bin/env python3
"""Every optional-integration assembly must be constrained, not merely flagged.

`versionDefines` defines a symbol WHEN a package is present. It does not stop the
assembly compiling when the package is absent: that is `defineConstraints`. Eight
assemblies had the first and not the second, so importing the bridge into a stock
Unity project failed to compile ALL Mosaic assemblies, including the core. The bridge
then never started, while the connector and the service both reported success.

The customer-visible failure was total and its cause invisible: no Mosaic output
anywhere, because the compiler stopped before producing any.

Usage: python3 scripts/validate_asmdefs.py
Exit 0 only when every optional integration is properly constrained.
"""
import glob
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8"); sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "packages", "com.mosaic.bridge")


def main():
    problems = []
    checked = 0
    for f in sorted(glob.glob(os.path.join(ROOT, "**", "*.asmdef"), recursive=True)):
        d = json.load(open(f, encoding="utf-8"))
        name = d.get("name", os.path.basename(f))
        vds = [v.get("define") for v in d.get("versionDefines", []) if v.get("define")]
        constraints = d.get("defineConstraints", [])
        rel = os.path.relpath(f, ROOT)

        if name.startswith("Mosaic.Bridge.Tools.") and vds:
            checked += 1
            if not constraints:
                problems.append(f"{name} ({rel}): declares {vds} but has no defineConstraints, "
                                f"so it compiles even when the package is absent")
            elif vds[0] not in constraints:
                problems.append(f"{name} ({rel}): constrained on {constraints}, "
                                f"expected its own symbol {vds[0]}")
    for p in problems:
        print("  " + p)
    print(f"{'ASMDEFS OK' if not problems else 'ASMDEF DRIFT'} - "
          f"{checked} optional integration(s) checked, {len(problems)} problem(s)")
    return 0 if not problems else 1


if __name__ == "__main__":
    sys.exit(main())
