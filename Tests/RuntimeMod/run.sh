#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
rimworld_root="$HOME/.local/share/Steam/steamapps/common/RimWorld"
test_root="$(mktemp -d /tmp/deepcaller-runtime.XXXXXX)"
test_link="$rimworld_root/Mods/DeepcallerRuntimeChecks"
if [[ -e "$test_link" || -L "$test_link" ]]; then
    [[ "$(readlink -f "$test_link")" == "$project_root/Tests/RuntimeMod" ]] || exit 1
else
    ln -s "$project_root/Tests/RuntimeMod" "$test_link"
fi
mkdir -p "$test_root/Config"
# Stamp the INSTALLED game version: a mismatched <version> makes 1.7 discard
# the whole active-mod list ("Resetting."), so nothing under test would load.
game_version="$(tr -d '\r\n' < "$rimworld_root/Version.txt")"
cat > "$test_root/Config/ModsConfig.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>$game_version</version>
  <activeMods>
    <li>brrainz.harmony</li>
    <li>ludeon.rimworld</li>
    <li>ludeon.rimworld.royalty</li>
    <li>oskarpotocki.vanillafactionsexpanded.core</li>
    <li>vanillaexpanded.vpsycastse</li>
    <li>smashphil.vehicleframework</li>
    <li>oskarpotocki.vanillavehiclesexpanded</li>
    <li>nidelese.vpe.deepcaller</li>
    <li>deepcaller.runtimechecks</li>
  </activeMods>
</ModsConfigData>
EOF
if [[ "${1:-}" == "--normal-mods" ]]; then
    python3 "$project_root/Tests/RuntimeMod/normal_mods.py" "$test_root/Config/ModsConfig.xml"
fi
if [[ "${1:-}" == "--epiphanies" ]]; then
    python3 - "$test_root/Config/ModsConfig.xml" <<'PY'
import sys
import xml.etree.ElementTree as ET
tree = ET.parse(sys.argv[1])
active = tree.getroot().find('activeMods')
item = ET.Element('li')
item.text = 'vanillaexpanded.vmemese'
active.insert(len(active) - 2, item)
tree.write(sys.argv[1], encoding='utf-8', xml_declaration=True)
PY
fi
echo "Isolated runtime log: $test_root/Player.log"
# 1.6 (Unity) ships RimWorldLinux; 1.7 (Waylith) ships a .NET apphost named RimWorld.
game_bin="$rimworld_root/RimWorldLinux"
[[ -x "$game_bin" ]] || game_bin="$rimworld_root/RimWorld"
exec "$game_bin" -batchmode -screen-fullscreen 0 -screen-width 1024 -screen-height 768 \
    "-savedatafolder=$test_root" -logFile "$test_root/Player.log" -deepcaller-tests "$@"
