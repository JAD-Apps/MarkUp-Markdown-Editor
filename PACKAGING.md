# MarkUp — Store Packaging & Upload Guide

This guide describes the steps to produce a Microsoft Store–ready MSIX package for **MarkUp Markdown Editor**.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| Visual Studio 2022+ with Windows App SDK workload | Or VS 2026 Insiders |
| Windows App SDK 1.8 | Installed via NuGet (automatic on build) |
| `MakePri.exe` / `makeappx.exe` | Included with the Windows SDK |
| Store Publisher certificate | Stored in `MarkUp Markdown Editor/Assets/` or via Partner Center |
| Microsoft Partner Center account | Required for Store submission |

> **IL Trimming is permanently disabled** (`PublishTrimmed=False` in the .csproj). Do not
> re-enable it — WinUI 3 / Windows App SDK are not trim-compatible and trimmed packages
> fail at runtime with `MissingMethodException`.

---

## Building the Store Package

### Via Visual Studio (recommended)

1. Set the **Solution Configuration** to **Release** and the **Solution Platform** to
   **x64** (repeat for **x86** and **ARM64** if producing a multi-arch bundle).
2. Right-click the `MarkUp Markdown Editor` project → **Publish** →
   **Create App Packages…**
3. Choose **Microsoft Store** → select or create your Partner Center app association.
4. Set the **Version** to match `<Version>` in the `.csproj` (e.g. `1.7.0.0`).
   The version in `Package.appxmanifest` must also match — both are updated automatically
   by the release process.
5. Select all three architectures (x86, x64, ARM64).
6. Click **Create** — VS produces an `.msixbundle` and per-arch `.appxsym` files under
   `AppPackages\StoreUpload\`.

### Via Command Line

```powershell
# From the repo root
dotnet build "MarkUp Markdown Editor\MarkUp Markdown Editor.csproj" `
	-c Release `
	-p:GenerateAppxPackageOnBuild=true `
	-p:AppxBundle=Always `
	-p:AppxBundlePlatforms="x86|x64|ARM64"
```

The output lands in `AppPackages\StoreUpload\`.

---

## Uploading to Partner Center

1. Sign in to [Partner Center](https://partner.microsoft.com/dashboard).
2. Select **MarkUp Markdown Editor** → **Start a new submission** (or update an existing draft).
3. In **Packages**, upload:
   - `MarkUp Markdown Editor_<version>.msixbundle`
   - The three `.appxsym` symbol files (one per arch)
4. Complete the **Store listing** and **Pricing/Availability** sections (no changes needed for
   a patch/minor release — the existing text carries over).
5. Click **Submit to the Store**.

---

## Release Checklist

- [ ] All unit tests pass (`dotnet test` → 400/400).
- [ ] `<Version>` in `MarkUp Markdown Editor.csproj` updated.
- [ ] `Package.appxmanifest` `Version` attribute updated (format `X.Y.Z.0`).
- [ ] `CHANGELOG.md` entry added with the correct date (`git log -1 --format=%ad --date=short`).
- [ ] `jad-apps-site/app.js` `changelogHighlights` updated (keep latest 2 entries).
- [ ] `PublishTrimmed` is `False` — verify before every Store build.
- [ ] Store package built and smoke-tested locally (sideload or clean VM).
- [ ] `.msixbundle` + `.appxsym` files uploaded to Partner Center.
- [ ] Submission certified and published.
- [ ] Git tag created: `git tag v<version> && git push origin v<version>`.
