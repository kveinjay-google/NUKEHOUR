# iOS Clean Engine Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a public iOS IPA that contains only redistributable engine/framework content and imports legally owned RA2/YR retail data locally through the iOS Files picker.

**Architecture:** Add a manifest-driven bundle audit and two explicit build modes. Keep retail-content parsing and transactional import in platform-neutral code, expose only file selection through UIKit, and block normal game startup until required retail packages are present. Public builds use an allowlist and are independently scanned after app/IPA generation.

**Tech Stack:** C#/.NET 8 iOS, OpenRA GPLv3 engine, UIKit document picker, JSON manifests, Python 3 build audit, NUnit, `xcrun devicectl`.

---

## File map

- Create `packaging/public-content-manifest.json`: authoritative redistributable-file classifications.
- Create `packaging/public_bundle_audit.py`: source/app/IPA scanner and report writer.
- Create `packaging/tests/test_public_bundle_audit.py`: scanner policy tests.
- Modify `ios/OpenRA.iOS/OpenRA.iOS.csproj`: `DevelopmentRetail` and `PublicClean` bundle item groups and audit target.
- Create `OpenRA.Mods.RA2/Content/RetailContentCatalog.cs`: required package names and readiness states.
- Create `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`: safe staging, hash, validation, and atomic publish.
- Create `OpenRA.Mods.RA2/Content/RetailImportModels.cs`: immutable import request/result types.
- Create `ios/OpenRA.iOS/IosRetailContentPicker.cs`: UIKit-only security-scoped file/folder picker.
- Create `ios/OpenRA.iOS/IosContentImportCoordinator.cs`: first-launch native wizard orchestration.
- Modify `ios/OpenRA.iOS/AppDelegate.cs`: run readiness/import before `Game.InitializeAndRun`.
- Modify `ios/OpenRA.iOS/ViewController.cs`: native import/status/error views.
- Modify `mods/ra2-content/mod.yaml` and installer YAML: local Files-source package declarations without downloads.
- Create `mods/ra2/fluent/zh-CN/content-import.ftl` and English counterpart: engine-side content status strings.
- Create `docs/legal/THIRD_PARTY_CONTENT.md`, `docs/legal/PUBLIC_DISTRIBUTION.md`, and `docs/legal/public-asset-sbom.json`.
- Create `ios/scripts/build-public-clean.sh`: reproducible signed build, audit, IPA and checksum.
- Add focused NUnit tests under `engine/OpenRA.Test/` and Python unit tests under `packaging/tests/`.

### Task 1: Establish the public resource inventory and deny-by-default audit

**Files:**
- Create: `packaging/public-content-manifest.json`
- Create: `packaging/public_bundle_audit.py`
- Create: `packaging/tests/test_public_bundle_audit.py`
- Create: `docs/legal/public-asset-sbom.json`

- [ ] **Step 1: Write failing scanner tests**

Create tests that build temporary app trees and assert that `.mix`, `.bag`, `.aud`, `.vxl`, `.hva`, retail maps, unlisted PNG/SHP files, Mach-O files outside the signed executable allowlist, and manifest hash mismatches fail. Assert that GPL engine DLLs, shaders, declared fonts, YAML rules, localization and declared original UI pass.

```python
class PublicBundleAuditTest(unittest.TestCase):
    def test_retail_mix_is_rejected(self):
        result = audit_tree(self.make_tree("mods/ra2/ra2.mix"), self.manifest)
        self.assertIn("retail-extension:.mix", result.errors)

    def test_unlisted_binary_art_is_rejected(self):
        result = audit_tree(self.make_tree("mods/ra2/bits/unit.shp"), self.manifest)
        self.assertIn("unclassified-binary", result.errors)

    def test_declared_original_asset_with_matching_sha_passes(self):
        path = self.write_asset("mods/ra2-public/icon.png", b"original")
        manifest = self.manifest.allow(path, sha256(path))
        self.assertEqual([], audit_tree(self.root, manifest).errors)
```

- [ ] **Step 2: Run the tests and confirm RED**

Run: `python3 -m unittest packaging.tests.test_public_bundle_audit -v`

Expected: import failure because `public_bundle_audit` does not exist.

- [ ] **Step 3: Implement the audit policy**

Implement `audit_tree(root, manifest)` and a CLI accepting `--root`, `--manifest`, `--json-report`, and `--markdown-report`. The scanner must normalize relative paths, reject symlinks escaping the root, hash every non-generated resource, and return exit code 2 when any file is missing classification or violates its build-mode policy.

```python
RETAIL_EXTENSIONS = {".mix", ".bag", ".idx", ".aud", ".vxl", ".hva", ".wsa", ".bik"}

@dataclass(frozen=True)
class AuditResult:
    errors: tuple[str, ...]
    files: tuple[dict, ...]

def audit_tree(root: Path, manifest: dict) -> AuditResult:
    allowed = {entry["path"]: entry for entry in manifest["files"]}
    errors, files = [], []
    for path in sorted(p for p in root.rglob("*") if p.is_file()):
        relative = path.relative_to(root).as_posix()
        if path.suffix.lower() in RETAIL_EXTENSIONS:
            errors.append(f"retail-extension:{path.suffix.lower()}:{relative}")
            continue
        entry = allowed.get(relative)
        if entry is None and is_resource(path):
            errors.append(f"unclassified-resource:{relative}")
            continue
        files.append(describe(path, relative, entry))
    return AuditResult(tuple(errors), tuple(files))
```

- [ ] **Step 4: Generate the first source inventory**

Run the scanner in inventory mode against `mods/ra2`, `engine/mods/common`, `engine/glsl`, and iOS app assets. Classify every file as `engine-source`, `project-original`, `redistributable-third-party`, `retail-user-content`, or `unknown-or-derivative`. Do not promote unknown files merely to make the audit pass.

- [ ] **Step 5: Verify GREEN and commit**

Run: `python3 -m unittest packaging.tests.test_public_bundle_audit -v`

Expected: all scanner tests pass.

Commit only Task 1 files with message `Audit public iOS bundle resources`.

### Task 2: Add explicit DevelopmentRetail and PublicClean bundle modes

**Files:**
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Create: `ios/scripts/build-public-clean.sh`
- Test: `engine/OpenRA.Test/IosPublicBundleProjectTest.cs`

- [ ] **Step 1: Write a failing project-structure test**

Assert that `PublicClean=true` is deny-by-default, does not include recursive `../../mods/ra2/**/*`, includes `mods/ra2-content`, and runs `public_bundle_audit.py` after app generation.

```csharp
StringAssert.Contains("<PublicClean Condition=", project);
StringAssert.Contains("Condition=\"'$(PublicClean)' != 'true'\"", project);
StringAssert.Contains("../../mods/ra2-content/**/*", project);
StringAssert.Contains("public_bundle_audit.py", project);
```

- [ ] **Step 2: Run RED**

Run: `DOTNET_ROLL_FORWARD=Major dotnet test engine/OpenRA.Test/OpenRA.Test.csproj -c Release --filter FullyQualifiedName~IosPublicBundleProjectTest`

Expected: assertions fail against the current recursive bundle globs.

- [ ] **Step 3: Implement conditional item groups**

Keep existing resources only when `PublicClean != true`. For `PublicClean=true`, include exact engine runtime files, audited common content, `mods/ra2-content`, and files carrying `Public=true` metadata from the manifest-generated MSBuild item list. Add an `AfterTargets="_CreateAppBundle"` audit target that fails on scanner exit code 2.

- [ ] **Step 4: Implement the reproducible public build script**

The script must resolve the repository root, reject a missing clean manifest, invoke signed `ios-arm64` Release with `-p:PublicClean=true`, audit the `.app`, create an IPA without modifying the app, audit the IPA, and emit SHA-256 plus JSON/Markdown reports under `artifacts/public-ios/`.

- [ ] **Step 5: Verify development success and public fail-closed behavior**

Run the focused test and build `DevelopmentRetail`; it must retain the current test content. Build `PublicClean`; until Tasks 6 and 8 finish classifying/replacing the required startup assets, it must stop at the audit target with a nonzero exit and a report naming every denied or unclassified file. A passing PublicClean build before those classifications is a test failure because it indicates that the gate was bypassed.

- [ ] **Step 6: Commit**

Commit Task 2 files with message `Separate public and retail iOS bundles`.

### Task 3: Model RA2/YR content readiness without touching UIKit

**Files:**
- Create: `OpenRA.Mods.RA2/Content/RetailContentCatalog.cs`
- Create: `OpenRA.Mods.RA2/Content/RetailImportModels.cs`
- Test: `engine/OpenRA.Test/RetailContentCatalogTest.cs`

- [ ] **Step 1: Write failing readiness tests**

Cover: empty directory, complete RA2 base, RA2 plus optional music, incomplete YR, and complete YR. Require explicit states for `Base`, `Yuri`, `Audio`, and `Maps`.

```csharp
Assert.That(catalog.Inspect(empty).Base, Is.EqualTo(ContentState.Missing));
Assert.That(catalog.Inspect(baseFiles).CanLaunchBaseGame, Is.True);
Assert.That(catalog.Inspect(incompleteYuri).Yuri, Is.EqualTo(ContentState.Incomplete));
```

- [ ] **Step 2: Run RED**

Expected: `RetailContentCatalog` type does not exist.

- [ ] **Step 3: Implement immutable readiness models**

Define `ContentState { Missing, Incomplete, Ready, Unsupported }`, `RetailContentStatus`, and a catalog containing case-insensitive file requirements. Base requires `ra2.mix` and `language.mix`; YR readiness requires `ra2md.mix` and `langmd.mix`; audio and maps report separately and never block base launch.

- [ ] **Step 4: Verify and commit**

Run focused tests and commit with message `Model RA2 retail content readiness`.

### Task 4: Implement transactional retail-content import

**Files:**
- Create: `OpenRA.Mods.RA2/Content/RetailContentImporter.cs`
- Test: `engine/OpenRA.Test/RetailContentImporterTest.cs`

- [ ] **Step 1: Write failing import tests**

Test accepted extension allowlist, executable rejection, cancellation, insufficient-space preflight, hash generation, duplicate import, interrupted staging cleanup, and preservation of the previous ready directory on failure.

```csharp
var result = await importer.ImportAsync(request, CancellationToken.None);
Assert.That(result.Published, Is.True);
Assert.That(File.Exists(Path.Combine(content, "ra2.mix")), Is.True);

var rejected = await importer.ImportAsync(request.WithFile("payload.dylib"), CancellationToken.None);
Assert.That(rejected.Error, Is.EqualTo(RetailImportError.ExecutableContent));
```

- [ ] **Step 2: Run RED**

Expected: importer type does not exist.

- [ ] **Step 3: Implement staging and atomic publish**

Copy into `Content/.staging/<session-id>`, reject files by extension and Mach-O magic, stream SHA-256 while copying, call `RetailContentCatalog.Inspect`, then rename current content to `.previous`, staging to `ra2`, and delete `.previous` only after readiness succeeds. Restore `.previous` on any publish failure.

- [ ] **Step 4: Verify and commit**

Run focused tests, `git diff --check`, and commit with message `Import retail content transactionally`.

### Task 5: Add the native iOS Files picker and first-launch wizard

**Files:**
- Create: `ios/OpenRA.iOS/IosRetailContentPicker.cs`
- Create: `ios/OpenRA.iOS/IosContentImportCoordinator.cs`
- Modify: `ios/OpenRA.iOS/ViewController.cs`
- Modify: `ios/OpenRA.iOS/AppDelegate.cs`
- Test: `engine/OpenRA.Test/IosContentImportContractTest.cs`

- [ ] **Step 1: Write a failing integration-contract test**

Assert that AppDelegate inspects content before calling `Game.InitializeAndRun`, that picker results flow through `RetailContentImporter`, and that cancel leaves the app in the import screen rather than showing a fatal black screen.

- [ ] **Step 2: Run RED**

Expected: native coordinator references are absent.

- [ ] **Step 3: Implement `IosRetailContentPicker`**

Use `UIDocumentPickerViewController` in import mode with data/content UTTypes and folder selection where supported. Balance every `StartAccessingSecurityScopedResource()` with `StopAccessingSecurityScopedResource()` and copy only through the importer.

- [ ] **Step 4: Implement the coordinator state machine**

Use states `Checking`, `NeedsBase`, `Importing`, `NeedsYuriOptional`, `Ready`, and `Error`. The coordinator owns no game renderer state; it calls the supplied `startGame` delegate exactly once after readiness.

- [ ] **Step 5: Build the landscape-native wizard**

Add large iPhone/iPad landscape buttons: `选择正版游戏文件`, `导入基础游戏`, `导入尤里的复仇`, `查看许可证`, and `重试`. Show per-category status and progress, with Dynamic Type-safe labels and no sliders.

- [ ] **Step 6: Verify and commit**

Run focused tests, build signed `ios-arm64`, and commit with message `Add iOS retail content import wizard`.

### Task 6: Make the bundled OpenRA content installer complete and offline-only

**Files:**
- Modify: `mods/ra2-content/mod.yaml`
- Modify: `mods/ra2-content/installer/ra2-disk.yaml`
- Modify: `mods/ra2-content/installer/origin.yaml`
- Modify: `mods/ra2-content/installer/firstdecade.yaml`
- Create: `mods/ra2-content/fluent/zh-CN/chrome.ftl`
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Test: `engine/OpenRA.Test/Ra2ContentInstallerManifestTest.cs`

- [ ] **Step 1: Write failing manifest tests**

Require RA2 and YR package definitions, no HTTP/HTTPS download URL, Chinese strings, and inclusion of `mods/ra2-content` in both iOS modes.

- [ ] **Step 2: Run RED**

Expected: missing YR/native iOS import declarations and missing bundled installer mod.

- [ ] **Step 3: Add offline package declarations**

Keep legacy desktop disc extraction, add file/folder import descriptions consumed by the native coordinator, define YR base/audio packages, and remove any automatic third-party retail-content download path from the public mode.

- [ ] **Step 4: Verify and commit**

Run focused tests and `./utility.sh --check-yaml`; commit with message `Complete offline RA2 content manifests`.

### Task 7: Enforce the map and executable-content boundary

**Files:**
- Create: `OpenRA.Mods.RA2/Content/PublicContentSafetyPolicy.cs`
- Modify: `mods/ra2/mod.yaml`
- Test: `engine/OpenRA.Test/PublicContentSafetyPolicyTest.cs`

- [ ] **Step 1: Write failing safety tests**

Reject imported DLL/dylib/framework/bundle/Mach-O, map packages containing external assemblies, and arbitrary mod directories. Accept retail MIX data and maps without executable extension hooks.

- [ ] **Step 2: Run RED**

Expected: safety policy type does not exist.

- [ ] **Step 3: Implement public-mode restrictions**

Expose `IsPublicClean` through launch arguments. In public mode, mount only the built-in code mod plus imported data packages; do not append user mod search paths or load assemblies named by imported manifests. Imported map rule/script overrides remain disabled until separately reviewed.

- [ ] **Step 4: Verify and commit**

Run focused tests and commit with message `Restrict executable content in public builds`.

### Task 8: Replace public branding and publish legal/source materials

**Files:**
- Modify: `ios/OpenRA.iOS/OpenRA.iOS.csproj`
- Modify: `ios/OpenRA.iOS/Info.plist`
- Create: `docs/legal/THIRD_PARTY_CONTENT.md`
- Create: `docs/legal/PUBLIC_DISTRIBUTION.md`
- Create: `mods/ra2-public/fluent/zh-CN/legal.ftl`
- Create: `mods/ra2-public/fluent/legal.ftl`
- Replace: `ios/OpenRA.iOS/Assets.xcassets/AppIcon.appiconset/*` with audited original public branding.
- Test: `engine/OpenRA.Test/PublicBrandingPolicyTest.cs`

- [ ] **Step 1: Write failing branding tests**

Assert that PublicClean title/icon metadata does not use EA/C&C logos or imply endorsement, contains the exact disclaimer, exposes GPLv3/source links, and identifies retail content as user-supplied.

- [ ] **Step 2: Run RED**

Expected: current `红色警戒：阿富汗危机` public title and assets fail policy.

- [ ] **Step 3: Add conditional public identity and notices**

Use the original public shell name `OpenRTS Mobile` and an audited project-original icon; retain the current development title only in `DevelopmentRetail`. Add the exact English disclaimer and Chinese explanation to the first-launch and legal screens. Document source retrieval, reproducible build command, third-party licenses, and no-warranty statement.

- [ ] **Step 4: Verify and commit**

Run focused tests and public bundle audit; commit with message `Add compliant public iOS identity and notices`.

### Task 9: Full clean-device verification and release evidence

**Files:**
- Create: `docs/testing/ios-public-clean-test-report.md`
- Create: `artifacts/public-ios/public-bundle-audit.json` during build (do not commit generated IPA).

- [ ] **Step 1: Build and audit**

Run `ios/scripts/build-public-clean.sh`. Expected: zero compiler errors, zero denied resources, signed arm64 app, IPA SHA-256 and complete SBOM report.

- [ ] **Step 2: Back up the current development container**

Use `xcrun devicectl device copy from` to preserve Documents/OpenRA before any clean-install test. Record the backup path and checksum. Do not delete the existing app until the backup is verified.

- [ ] **Step 3: Verify empty first launch**

Install PublicClean on the iPad with no content container. Expected: landscape Chinese import wizard, no crash, no RA2 main menu, and no network request for retail assets.

- [ ] **Step 4: Verify RA2 import**

Import a legally owned RA2 file set through Files. Expected: readiness becomes Base Ready; Allied and Soviet skirmishes load offline; music remains unavailable if not imported.

- [ ] **Step 5: Verify YR import**

Import the legally owned YR set. Expected: Yuri readiness becomes Ready and a Yuri skirmish loads with correct models, audio and maps.

- [ ] **Step 6: Verify recovery cases**

Exercise cancel, incomplete source, corrupted file, duplicate import, low-space simulation, background/resume, and interrupted import. Expected: previous ready content remains playable and no staging directory is mounted.

- [ ] **Step 7: Final report and commit**

Record build hash, app hash, device/iOS version, audit counts, import source edition, pass/fail matrix, screenshots and diagnostics. Commit documentation and focused fixes with message `Verify clean iOS engine distribution`.

## Plan self-review

- All nine approved recommendations map to Tasks 1–9.
- Public and development modes remain independently testable.
- Retail files never enter Git or the public build tree.
- Imported content is data-only and transactionally published.
- App Store/TestFlight legal review remains an explicit release gate and is not treated as unfinished engineering work.
