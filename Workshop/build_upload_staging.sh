#!/usr/bin/env bash
set -euo pipefail

# Build a lean Steam upload folder without Source/, Art/, .git/ or Workshop/.
# An existing staging folder is moved to a timestamped backup, never deleted.

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_root="${DEEPCALLER_WORKSHOP_STAGING:-/home/elyn/projects/VPE-Deepcaller-WorkshopUpload}"
stage_parent="$(dirname "$stage_root")"
temp_root="$(mktemp -d "$stage_parent/.VPE-Deepcaller-WorkshopUpload.XXXXXX")"

if [[ "$stage_root" == "/" || "$stage_root" == "$project_root" ]]; then
    echo "Refusing unsafe staging target: $stage_root" >&2
    exit 1
fi

dotnet build "$project_root/Source/Deepcaller/Deepcaller.csproj" --no-restore

for directory in Common 1.6 1.7 About Languages Textures; do
    if [[ ! -d "$project_root/$directory" ]]; then
        echo "Missing required runtime directory: $directory" >&2
        exit 1
    fi
    cp -a "$project_root/$directory" "$temp_root/"
done
# LoadFolders.xml is what lets one upload serve both 1.6 and 1.7 subscribers.
cp -a "$project_root/LoadFolders.xml" "$temp_root/"
for version in 1.6 1.7; do
    if [[ ! -s "$temp_root/$version/Assemblies/Deepcaller.dll" ]]; then
        echo "Missing $version/Assemblies/Deepcaller.dll" >&2
        exit 1
    fi
done

# The first RimWorld upload creates this ID in the staging copy. Preserve it
# across subsequent clean rebuilds even before it has been copied into git.
if [[ -f "$stage_root/About/PublishedFileId.txt" \
      && ! -f "$temp_root/About/PublishedFileId.txt" ]]; then
    published_id="$(tr -d '[:space:]' < "$stage_root/About/PublishedFileId.txt")"
    if [[ "$published_id" =~ ^[0-9]+$ ]]; then
        cp "$stage_root/About/PublishedFileId.txt" \
            "$temp_root/About/PublishedFileId.txt"
    fi
fi

preview="$temp_root/About/Preview.png"
if [[ ! -f "$preview" ]]; then
    echo "Missing About/Preview.png" >&2
    exit 1
fi
preview_bytes="$(stat -c '%s' "$preview")"
if (( preview_bytes >= 1000000 )); then
    echo "About/Preview.png exceeds Steam's 1 MB limit: $preview_bytes bytes" >&2
    exit 1
fi

xmllint --noout \
    "$temp_root/About/About.xml" "$temp_root/LoadFolders.xml" \
    $(find "$temp_root/Common" "$temp_root/Languages" \
        -type f -name '*.xml' -print)

if [[ -e "$stage_root" ]]; then
    backup_root="$stage_root.previous-$(date +%Y%m%d-%H%M%S)"
    mv "$stage_root" "$backup_root"
    echo "Previous staging copy preserved at: $backup_root"
fi

mv "$temp_root" "$stage_root"
echo "Steam upload staging copy ready: $stage_root"
du -sh "$stage_root"
