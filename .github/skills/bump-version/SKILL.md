---
name: bump-version
description: 'Bump the Nexplorer application version consistently across every file that holds a version string. Use when the user says "bump version", "release X.Y.Z", "update to vX.Y.Z", "new patch/minor/major version", or asks to ship a build. Updates Nexplorer.App.csproj, installer/Package.wxs (4-part), docs/version.json, docs/index.html (heading + download link), and README.md (two download links). DO NOT use for editing test fixtures — UpdateServiceTests.cs uses fixed regression values.'
argument-hint: '<new-version, e.g. 1.0.20>'
---

# Bump Nexplorer Version

## When to Use
- User asks to bump, increment, or set the application version.
- Preparing a release (combine with the [release](../release/SKILL.md) skill afterwards).
- Update checker reports a stale `docs/version.json`.

## When NOT to Use
- Editing tests under `tests/Nexplorer.Tests/` (especially `UpdateServiceTests.cs`) — those use frozen version values for regression coverage. Leave them alone.
- Bumping NuGet package versions (different concern).

## Files to Update

All files MUST share the same `Major.Minor.Build` triple. The MSI requires a 4-part version — append `.0` as Revision.

| File | Field | Format |
|------|-------|--------|
| [src/Nexplorer.App/Nexplorer.App.csproj](../../../src/Nexplorer.App/Nexplorer.App.csproj) | `<Version>` | `X.Y.Z` |
| [installer/Package.wxs](../../../installer/Package.wxs) | `Version` attribute on `<Package>` | `X.Y.Z.0` |
| [docs/version.json](../../../docs/version.json) | `"version"` and `"releaseNotes"` | `"X.Y.Z"` |
| [docs/index.html](../../../docs/index.html) | `<span class="ver">vX.Y.Z</span>` heading and "Download Nexplorer vX.Y.Z" link text | `vX.Y.Z` |
| [README.md](../../../README.md) | Both `Download v1.0.19` link text occurrences (top badge + bottom CTA) | `vX.Y.Z` |

## Procedure

1. **Discover the current version.** Read `Nexplorer.App.csproj` and confirm the existing `<Version>` value.
2. **Confirm the new version** with the user if not already supplied, or infer from semver bump intent (patch/minor/major).
3. **Apply edits in a single batch** using `multi_replace_string_in_file` — one operation per file. Always include enough surrounding context so each replacement is unambiguous.
4. **Update `docs/version.json` `releaseNotes`.** If the user provided release notes, use them verbatim; otherwise default to `"Version bump to X.Y.Z."` (matches existing convention).
5. **Verify** by re-reading each touched file or running `grep_search` for the OLD version string across the workspace — there should be **zero** remaining matches outside `tests/`.
6. **Do not commit, tag, or push.** Stop after the file edits. Use the [release](../release/SKILL.md) skill if the user wants to actually ship.

## Verification Checklist

- [ ] `.csproj` has `<Version>X.Y.Z</Version>`
- [ ] `Package.wxs` has `Version="X.Y.Z.0"` (4-part)
- [ ] `version.json` has matching `"version"` and updated `"releaseNotes"`
- [ ] `index.html` heading span and download link both show `vX.Y.Z`
- [ ] `README.md` contains `vX.Y.Z` in both link occurrences
- [ ] `grep_search` for the old `X.Y.Z` returns only matches under `tests/`

## Common Pitfalls

- **Forgetting the `.0` revision in `Package.wxs`** — WiX requires 4-part versions; the build will fail otherwise.
- **Forgetting `docs/version.json`** — the in-app update checker reads this at runtime. Users will silently miss the update.
- **Editing only one of the two README occurrences** — both the top badge and bottom CTA must match.
- **Touching `UpdateServiceTests.cs`** — its hardcoded versions are intentional. Skip it.
- **Using sequential single-file edits** — prefer one `multi_replace_string_in_file` call so the changes are atomic and easy to review.
