"""Generate disposable verification projects from this project's Unity references."""
from pathlib import Path
import xml.etree.ElementTree as ET
import subprocess

root = Path(__file__).resolve().parents[2]
output = root / "Temp" / "PlacementVerification"
runner = root / "Temp" / "PlacementCoreTests"
output.mkdir(parents=True, exist_ok=True)
runner.mkdir(parents=True, exist_ok=True)
source = ET.parse(root / "Assembly-CSharp.csproj").getroot()
project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
properties = ET.SubElement(project, "PropertyGroup")
for key, value in {
    "TargetFramework": "netstandard2.1",
    "EnableDefaultItems": "false",
    "GenerateAssemblyInfo": "false",
    "LangVersion": "9.0",
    "DefineConstants": source.find(".//DefineConstants").text,
    "NoWarn": "0169;0649;0618;0414;0108;0114;0162;0219;MSB3277",
    "AssemblyName": "PlacementVerification",
}.items():
    ET.SubElement(properties, key).text = value
items = ET.SubElement(project, "ItemGroup")
files = {str(root / n.attrib["Include"]) for n in source.findall(".//Compile")}
files.update(str(f) for f in (root / "Assets/Game Assets/Scripts/Placement").rglob("*.cs"))
for path in sorted(files):
    ET.SubElement(items, "Compile", Include=path)
core_module = None
for node in source.findall(".//Reference"):
    hint = node.find("HintPath")
    if hint is None:
        continue
    path = Path(hint.text)
    if not path.is_absolute():
        path = root / path
    if node.attrib["Include"] == "UnityEngine.CoreModule":
        core_module = path
    if "NetStandard" in str(path) or "netstandard" in path.name or path.name in ("mscorlib.dll", "System.dll", "System.Core.dll"):
        continue
    reference = ET.SubElement(items, "Reference", Include=node.attrib["Include"])
    ET.SubElement(reference, "HintPath").text = str(path)
for node in source.findall(".//ProjectReference"):
    name = Path(node.attrib["Include"]).stem
    path = root / "Library/ScriptAssemblies" / (name + ".dll")
    if path.exists():
        reference = ET.SubElement(items, "Reference", Include=name)
        ET.SubElement(reference, "HintPath").text = str(path)
ET.ElementTree(project).write(output / "PlacementVerification.csproj", encoding="utf-8", xml_declaration=True)

if core_module is None:
    raise RuntimeError("UnityEngine.CoreModule was not found in the generated Unity project.")
project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
properties = ET.SubElement(project, "PropertyGroup")
for key, value in {"TargetFramework": "net9.0", "OutputType": "Exe", "EnableDefaultItems": "false"}.items():
    ET.SubElement(properties, key).text = value
items = ET.SubElement(project, "ItemGroup")
ET.SubElement(items, "Compile", Include=str(root / "Tools/Placement/CoreChecks.cs"))
for name, path in {
    "PlacementVerification": output / "bin/Debug/netstandard2.1/PlacementVerification.dll",
    "UnityEngine.CoreModule": core_module,
}.items():
    reference = ET.SubElement(items, "Reference", Include=name)
    ET.SubElement(reference, "HintPath").text = str(path)
ET.ElementTree(project).write(runner / "PlacementCoreTests.csproj", encoding="utf-8", xml_declaration=True)

for command, log in [
    (["dotnet", "build", str(output / "PlacementVerification.csproj"), "-v:quiet"], output / "build.log"),
    (["dotnet", "run", "--project", str(runner / "PlacementCoreTests.csproj")], runner / "results.log"),
]:
    with log.open("w", encoding="utf-8") as stream:
        result = subprocess.run(command, cwd=root, stdout=stream, stderr=subprocess.STDOUT)
    lines = log.read_text(encoding="utf-8").splitlines()
    print("\n".join(lines[-30:] if result.returncode else lines[-5:]))
    if result.returncode:
        raise SystemExit(result.returncode)
