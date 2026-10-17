"""ReportGenerator JsonSummary dosyasından kapsam eşiklerini doğrular."""
import json
import sys

TOTAL_MIN = 70.0
ASSEMBLY_MIN = {"SahaNet.Application": 85.0}

data = json.load(open(sys.argv[1], encoding="utf-8"))
total = data["summary"]["linecoverage"]
failures = []
print(f"Toplam satır kapsamı: {total:.1f}% (eşik {TOTAL_MIN}%)")
if total < TOTAL_MIN:
    failures.append(f"toplam {total:.1f}% < {TOTAL_MIN}%")

for assembly in data["coverage"]["assemblies"]:
    print(f"  {assembly['name']}: {assembly['coverage']:.1f}%")
    minimum = ASSEMBLY_MIN.get(assembly["name"])
    if minimum is not None and assembly["coverage"] < minimum:
        failures.append(f"{assembly['name']} {assembly['coverage']:.1f}% < {minimum}%")

if failures:
    print("Kapsam eşiği karşılanmadı: " + "; ".join(failures))
    sys.exit(1)
