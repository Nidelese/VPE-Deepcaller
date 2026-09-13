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
cat > "$test_root/Config/ModsConfig.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>1.6.4633 rev1230</version>
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
echo "Isolated runtime log: $test_root/Player.log"
exec "$rimworld_root/RimWorldLinux" -batchmode -screen-fullscreen 0 -screen-width 1024 -screen-height 768 \
    "-savedatafolder=$test_root" -logFile "$test_root/Player.log" -deepcaller-tests "${1:-}"
