#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_root="${DEEPCALLER_WORKSHOP_STAGING:-/home/elyn/projects/VPE-Deepcaller-WorkshopUpload}"
source_id="$stage_root/About/PublishedFileId.txt"
target_id="$project_root/About/PublishedFileId.txt"

if [[ ! -f "$source_id" ]]; then
    echo "RimWorld has not created a PublishedFileId.txt in the staging copy yet." >&2
    exit 1
fi

published_id="$(tr -d '[:space:]' < "$source_id")"
if [[ ! "$published_id" =~ ^[0-9]+$ ]]; then
    echo "Invalid Steam Workshop ID: $published_id" >&2
    exit 1
fi

if [[ -f "$target_id" ]]; then
    existing_id="$(tr -d '[:space:]' < "$target_id")"
    if [[ "$existing_id" != "$published_id" ]]; then
        echo "Refusing to replace existing Workshop ID $existing_id with $published_id" >&2
        exit 1
    fi
fi

cp "$source_id" "$target_id"
echo "Recorded Steam Workshop item ID $published_id in About/PublishedFileId.txt"
