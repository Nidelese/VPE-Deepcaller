# Deepcaller Steam Workshop page

## Prepared identity

- Title: `[1.6] Vanilla Psycasts Expanded — Deepcaller`
- Short description: see `short_description.txt`
- Tags: `Mod`, `1.6`
- Full Steam BBCode: `description.bbcode`
- Initial change note: `change_note_1.0.0.txt`
- Primary preview: `../About/Preview.png` (640×360, below 1 MB)
- Large hero and optional description banners: `Assets/`

The page copy identifies Deepcaller as an unofficial VPE add-on, credits
Nidelese, thanks Oskar Potocki and the Vanilla Expanded team for the
Vanilla Psycasts Expanded foundation, credits Codex as Nidelese's AI
collaborator, links the public source repository, and places the Patreon link
only in a clearly optional donation section.

## Required Workshop items

The description contains a large, explicit **REQUIRED DEPENDENCIES** section.
After the first upload, also use Steam's **Add/Remove Required Items** control
so Steam can offer players the dependencies automatically:

1. Harmony — `2009463077`
2. Vanilla Expanded Framework — `2023507013`
3. Vanilla Psycasts Expanded — `2842502659`

## Safe first-upload sequence

The vanilla RimWorld uploader operates on the local mod folder. The project
also contains 128 MB of production art and source material that players do not
need, so do not upload the development tree directly.

1. Close RimWorld if it is running.
2. Build and select the lean upload copy:

   ```bash
   ./Workshop/build_upload_staging.sh
   ./Workshop/use_upload_copy.sh
   ```

3. Launch RimWorld through Steam.
4. Enable **Development mode** in Options.
5. Open **Mods**, select Deepcaller, open **Advanced**, then choose
   **Upload on Steam**.
6. Steam creates the new item and writes `About/PublishedFileId.txt` into the
   staging copy. New items may begin hidden and may require accepting the
   [Steam Workshop Legal Agreement](https://steamcommunity.com/sharedfiles/workshoplegalagreement).
7. Immediately preserve the ID:

   ```bash
   ./Workshop/capture_published_id.sh
   ```

   Never delete or substitute this ID for later updates; it is what prevents
   RimWorld from creating a duplicate Workshop item.

8. On the Workshop item page:

   - paste `description.bbcode`;
   - set tags to `Mod` and `1.6`;
   - add the three required items above;
   - paste `change_note_1.0.0.txt` as the first change note;
   - set visibility to **Public** after reviewing the page.

9. Restore the live development copy:

   ```bash
   ./Workshop/use_dev_copy.sh
   ```

## Embedded banners

Steam's item description can embed hosted images with `[img]URL[/img]`. The
three final banners are attached to the public GitHub `v1.0.0` release, and
`description.bbcode` embeds those stable release URLs directly:

- `Assets/Deepcaller_DescriptionBanner_1280x320.png`
- `Assets/Deepcaller_FeatureStrip_1280x320.png`
- `Assets/Deepcaller_OptionalSupport_1280x320.png`

The optional-support art is also supplied as a standalone cutout:

- `Assets/Deepcaller_PatreonMascot.png` — transparent Leviathan mascot cutout.

The support banner is wrapped in a Patreon URL, making the little busker itself
clickable.

Keep the accompanying wording from `description.bbcode`: support is optional,
the mod is free and complete, and nothing is locked behind donations.

## Future updates

1. Keep `About/PublishedFileId.txt` in the project.
2. Rebuild and select the staging copy.
3. Use RimWorld's upload button; it will update the existing item.
4. Add a concise change note.
5. Switch back to the development copy.

Steam officially documents that Workshop uploads set title, description,
visibility, tags, content and the primary preview image, and that a successful
first submission returns the `PublishedFileId` used for later updates:
https://partner.steamgames.com/doc/features/workshop/implementation
