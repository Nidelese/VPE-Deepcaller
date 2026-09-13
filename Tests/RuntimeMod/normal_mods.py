"""Copy the player's active mod list into the disposable harness profile."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

source = Path.home() / '.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Config/ModsConfig.xml'
tree = ET.parse(source)
active = tree.getroot().find('activeMods')
for package in ('oskarpotocki.vanillavehiclesexpanded', 'deepcaller.runtimechecks'):
    if package not in [item.text.lower() for item in active]:
        ET.SubElement(active, 'li').text = package
tree.write(sys.argv[1], encoding='utf-8', xml_declaration=True)
