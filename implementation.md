# CreateFile implementation status

Last updated: 2026-10-12. This document maps the original requirements to the work performed in this workspace and identifies what remains.

**Overall status: a working first version, not yet a production-validated release.** The x64 application creates files through a native WinUI interface. The strict core suite passes with zero skips, including ACL denial (51 tests), and the native COM DLL passes its standalone smoke test. An unsigned MSIX passes SDK validation. On Windows 11 build 26300 x64, both the modern and classic Explorer integrations have been verified live; see [P0 verification](docs/P0-VERIFICATION.md#observed-results-windows-11-build-26300-x64). Two packaging defects were fixed during that run.

Status terms used below:

- **Implemented and verified:** relevant build, automated test, or direct UI evidence exists. This does not imply every Windows version or edge case has been tested.
- **Implemented; verification pending:** source code exists, but the corresponding integration or complete user flow has not been exercised successfully.
- **Partial:** some requested behavior exists, with a specific gap noted.
- **Pending:** not implemented or not completed.
- **Deferred by request:** intentionally left unconfigured at the user's direction.

## 1. Repository inspection and technology choices

| Requirement | Work completed | Status / remaining work |
| --- | --- | --- |
| Inspect before changing files | Inspected the existing solution, App/Core/CLI templates, installed SDKs, NuGet cache, Visual Studio, and Windows SDK tools. | Completed. |
| Reuse existing projects | Reused `CreateFile.slnx` and all three original managed projects. Replaced template code with working implementation; added Shell and tests. | Completed. |
| C# and .NET 10 | Core/CLI target .NET 10; App targets `net10.0-windows10.0.26100.0`. Local SDK was 10.0.401. | x64 builds verified. |
| WinUI 3 / Windows App SDK | Native XAML application; pinned WinUI 2.3.9, InteractiveExperiences 2.1.9, and Windows App Runtime 2.5.1 packages. | Built and launched. Full supported-OS matrix pending. |
| Native C++ / COM | MSVC v143 DLL implements `IExplorerCommand` and `IObjectWithSite`. | x64 compiled and standalone COM tests passed. |
| No Electron or web interface | No Electron, HTML/CSS UI, or browser control is used. | Implemented. WinUI brings transitive WebView2 interop dependencies, but the app does not instantiate a browser. |
| x64 and ARM64 | Project configurations, publish profiles, build script, and CI matrix cover x64/ARM64. Removed the unused x86 publish profile. | x64 verified. The ARM64 build passes in GitHub CI (default platform toolset); device testing pending. |
| Official Microsoft integration mechanisms | Consulted Microsoft documentation for package extensions, COM registration, item-type schema, `IExplorerCommand.GetState`, and App SDK versions. References are linked in the README. | Supported mechanisms implemented; registration and live menus verified on Windows 11 26300 x64. |

## 2. Explorer context-menu integration

### Modern Windows 11 menu

Implemented in [CreateFile.Shell.cpp](src/CreateFile.Shell/CreateFile.Shell.cpp) and [AppxManifest.xml](packaging/AppxManifest.xml):

- Native COM class factory and `DllGetClassObject` / `DllCanUnloadNow` exports.
- Command title `Create New File`, canonical command GUID, state queries, flags, and invocation.
- `IObjectWithSite` support to obtain the current Explorer folder for background invocation.
- Selected-folder resolution through `IShellItemArray`.
- Desktop virtual-root handling through the Desktop known folder.
- Hiding/disabling unsupported targets and rejecting ambiguous multi-folder selections.
- `E_PENDING` on the fast `GetState` path instead of doing folder resolution there.
- Launches the sibling WinUI executable using `CreateProcessW`, without a command interpreter or waiting for the dialog to finish.
- Windows argument quoting, including trailing backslashes.
- Reference counting, COM pointer ownership, handle cleanup, and exception containment at invocation.
- Manifest declarations for `desktop4:FileExplorerContextMenus`, `desktop5:ItemType` (`Directory` and `Directory\Background`), and `com:SurrogateServer`.
- No unsupported registry hack to force modern-menu placement.

**Status: implemented and verified on Windows 11 26300 x64.** The early sandboxed attempts were access-denied (`0x80070005`), but registration succeeded in a normal user session. The live run then found and fixed two problems. First, the dialog did not open, because the WinAppSDK bootstrapper fails when package identity is inherited; the fix is `OnPackageIdentity_NoOp`. Second, settings writes were virtualized; the fix is `desktop6:FileSystemWriteVirtualization`. After those fixes, the command appears once on folder objects, folder backgrounds and the desktop, with correct targets, and is absent on virtual locations and multi-selection. Windows 10 and ARM64 remain untested. The command now supplies an icon: `GetIcon` returns the packaged `Assets\AppIcon.ico`. The smoke tests check this, but nobody has yet seen the icon live in the Explorer menu.

### Classic Windows 10/11 menu

Implemented in [ShellIntegration.cs](src/CreateFile.App/Services/ShellIntegration.cs) and [Classic-Integration.ps1](scripts/Classic-Integration.ps1):

- Per-user entries for folder backgrounds, folder objects, and the desktop.
- Registry scope: `HKCU\Software\Classes\{Directory\Background,Directory,DesktopBackground}\shell\CreateFile.New`.
- Quoted executable path and quoted `%1` / `%V` target; a `\.` suffix handles terminal directory separators.
- The same `--folder` activation and WinUI dialog as the native command.
- Enable, repair, remove, and diagnostic operations.
- Classic integration is opt-in to reduce duplicates when packaged verbs already appear in the classic menu.

**Status: implemented; Windows 11 verified, Windows 10 pending.** On Windows 11 26300, enable, repair and remove work. Classic entries launch unpackaged with correct targets on folder background, folder object, drive root and desktop. With the package also installed, Show more options lists the command twice. Repair does not add more entries. Automatic duplicate detection is not implemented; the current approach is opt-in registration plus guidance.

## 3. File creation engine

Implemented in [FileCreationService.cs](src/CreateFile.Core/FileCreationService.cs).

| Requirement | Implementation / evidence | Status |
| --- | --- | --- |
| Arbitrary extensions | No extension allowlist; valid filenames are accepted. Normal and custom extensions tested. | Implemented and verified. |
| Extensionless files and dotfiles | Supports `Dockerfile`, `.env`, `.gitignore`, and similar names. | Implemented and verified. |
| Unicode filenames | Valid Unicode accepted; malformed surrogate sequences rejected. Automated Unicode case and actual WinUI creation checked. | Implemented and verified. |
| Custom filename and target directory | Filename field, editable target field, native folder picker, and `--folder` argument. | Direct target entry and creation verified; complete picker flow pending. |
| Never overwrite silently | Uses atomic `FileMode.CreateNew`, not an existence-check/write sequence. Tests cover existing files, directory collisions, and concurrent creators. | Implemented and verified. |
| Windows filename validation | Rejects invalid/control characters, path separators, traversal names, ADS colon, trailing dots/spaces, reserved devices and superscript device digits, oversized names, and invalid Unicode. | Automated validation coverage passed. |
| Permission and filesystem errors | Returns errors for unavailable/nonexistent folders, access denial, IO failures, invalid paths, and cancellation. | Missing-folder and collision behavior tested. ACL test skipped; disk-full, offline-share, and read-only-volume tests pending. |
| Empty files | Creates zero-byte files. | Automated and UI checks passed. |
| Optional text content | Writes literal UTF-8 without BOM; maximum 1 MiB of template text. | Automated content and size checks passed. |
| Structured binary formats | Explicit UI message says a zero-byte PDF/DOCX/PNG/ZIP is not a valid structured document. No fake binary-format generation. | Implemented; message inspected in UI. |
| No elevation for ordinary creation | No UAC/elevation request in the creation flow. | Implemented; normal UI file creation succeeded. |
| Explorer refresh | Calls `SHChangeNotify` after successful creation. | Implemented; new files appear in Explorer without F5 (Windows 11 26300). |

Known behavior: a write failure or cancellation after creation can leave a partial **new** file. The app reports this instead of deleting by path and risking removal of another process's replacement. Long-path acceptance remains dependent on Windows, runtime, and filesystem behavior.

## 4. Native WinUI interface

Implemented in [MainWindow.xaml](src/CreateFile.App/MainWindow.xaml), its code-behind, [CreateFileViewModel.cs](src/CreateFile.App/ViewModels/CreateFileViewModel.cs), and [WindowLayout.cs](src/CreateFile.App/WindowLayout.cs).

| Requested behavior | Current implementation | Status / gap |
| --- | --- | --- |
| Fluent, native Windows appearance | WinUI controls, Fluent settings icon, Mica backdrop, native control styling, custom application icon. | Dialog and settings visually inspected. |
| Rounded corners | Uses standard WinUI control and Windows window styling. | No separate custom corner implementation; OS-version behavior needs testing. |
| Light / Dark / System | Persisted theme choice mapped to WinUI theme values; System follows framework behavior. | Implemented; exhaustive switching, OS-theme-change, and high-contrast testing pending. |
| Compact responsive dialog | Scrollable content, DPI-scaled dimensions bounded by display work area. | Checked at the current desktop DPI; small-screen and multi-monitor matrix pending. |
| Filename and target folder | Editable controls with accessible names. | Direct input verified. |
| Extension suggestions and favorites | Searchable preset selector with pinned entries first. | Implemented as a selector, not inline filename autocomplete. Full interaction testing pending. |
| Optional template selector | Built-ins and custom templates, with an always-available Empty file option. | Implemented; core template content tested. |
| Create and Cancel | Validation/busy state gates creation; errors stay in the dialog. | Creation verified. |
| Enter and Escape | Enter from filename creates when valid; Escape closes when not busy. | Both exercised successfully. |
| Close automatically | Controlled by persisted setting. | Verified after successful UI creation. |
| Inline validation and errors | Bound error message and disabled Create button for invalid names. | `CON.txt` rejection verified in the UI. |
| Accessibility | Standard controls, explicit names, keyboard support, polite live validation text. | Accessibility tree inspected; Narrator and complete keyboard audit pending. |
| Subtle animations | Standard control behavior only. | No custom transition/animation work or reduced-motion audit completed. |

Final end-to-end check: entered the workspace test folder, entered `ui-created-验证.env`, pressed Enter, confirmed a zero-byte file, and confirmed automatic close. Evidence is under `artifacts/ui-smoke/`.

The native picker opens, but the automation helper could not reliably target its broker window. Complete folder selection and template import/export picker interactions still require manual verification.

## 5. Presets and favorites

Implemented in [BuiltIns.cs](src/CreateFile.Core/BuiltIns.cs), configuration models, and the settings window.

All requested presets are included: Python, JavaScript, TypeScript, TSX, JSX, HTML, CSS, SCSS, JSON, Markdown, YAML, XML, C, C++, C#, Java, Rust, Go, PowerShell, SQL, Bash, and plain text. Additional presets cover `.env`, `.gitignore`, and `Dockerfile`.

- Add a custom type/extension and its default filename.
- Update or delete a preset.
- Pin/unpin favorites.
- Move presets up/down; pinned items appear first in the creation dialog while preserving their relative configured order.
- Search file types: live suggestions match name, extension, or default filename (for example `python`, `.md`, `gitignore`). Enter or a click applies the match ([PresetSearch.cs](src/CreateFile.Core/PresetSearch.cs)).
- Persist the ordered preset list and pin state in JSON.

**Status:** implemented. Every built-in default filename was exercised through the core engine, and configuration round-trip tests passed. Complete UI management and restart-persistence flows remain to be tested manually. Frequently used types are pinned manually; usage-based automatic ranking is not implemented.

## 6. Text templates

Included starters: HTML5 document, Python main, JavaScript starter, React TSX component, JSON object, Markdown README, C++ main, and C# program.

- Add, edit, and delete templates in settings.
- Import/export JSON containing ID, name, and literal content.
- Assign fresh IDs on import to avoid identity collisions.
- Reject malformed templates and oversized content/imports.
- Core exports use exclusive creation and reject collisions; UI export uses the save picker's replacement confirmation.
- Keep Empty file available independently of the template collection.
- Never expand commands, evaluate code, or execute template contents.

**Status:** core template creation, all built-in contents, import/export, malformed import, and oversized text checks passed. The settings UI is implemented; complete picker-based import/export and editing flows need manual testing.

## 7. Settings and configuration

| Area | Completed | Pending / limitations |
| --- | --- | --- |
| General | Theme, default filename, close after creation, open after creation, success-feedback preferences. | Full preference/restart UI matrix pending. |
| Open after creation | Uses Notepad with safe argument passing. | Intentionally opens as text rather than running a script/executable association. Manual association-safety checks pending. |
| Success notifications | In-dialog success InfoBar and preference. | No system toast; feedback disappears when automatic close is enabled. |
| Context menu | Classic enable/repair/remove; package-identity status; modern install/reinstall/removal explanation. | Status is not a live COM/Explorer health check. Modern repair/removal requires package deployment/uninstall, not an in-app repair operation. |
| File types | Add/update/delete, default names, favorites, ordering. | End-to-end management testing pending. |
| Templates | Add/edit/delete/import/export. | End-to-end picker testing pending. |
| About | Version, MIT license identification, copyright, repository/update controls. | Implemented: repository is https://github.com/workingpayload/Mint. |
| Updates | Code opens the repository's latest release page. | No version comparison, update download, or automatic updater. The release page has no releases yet. |
| JSON persistence | Schema version, input validation, size limits, unique temporary files and atomic replacement. | Simultaneous settings windows use last-successful-save semantics; no cross-process merge. |
| Invalid configuration | Defaults plus warning; original file preserved on load. Unsupported versions also handled. | Explicitly saving replaces the original; no automatic backup history. |
| Failed settings save | Changes are published to shared app settings only after successful persistence. | Broader storage-failure testing pending. |

Configuration location is based on `Environment.SpecialFolder.LocalApplicationData/CreateFile/settings.json`; packaged filesystem redirection can affect the physical path. Configuration/import JSON is limited to 8 MiB.

## 8. Architecture and source organization

| Component | Work completed |
| --- | --- |
| `src/CreateFile.App` | WinUI activation, creation dialog, creation view model, settings window, theme/window sizing, classic integration service. |
| `src/CreateFile.Core` | Filename validation, asynchronous creation, presets/templates, configuration persistence, template import/export. |
| `src/CreateFile.Cli` | `create`, `validate`, and `presets`; optional template import and nonzero error exits. |
| `src/CreateFile.Shell` | Native COM DLL, module exports, standalone native smoke-test executable. |
| `tests/CreateFile.Core.Tests` | Core test cases plus a dependency-free executable test runner. |
| `packaging` | MSIX manifest with package identity and Explorer/COM declarations. |
| `scripts` | Build, package, sign, install, uninstall, integration repair/diagnostics, native tests, icon generation. |
| `assets` | SVG logo source; generated icon/package assets are in the App project. |
| `.github/workflows` | x64/ARM64 Windows build/test/package workflow. |
| Documentation / license | README, verification checklist, file inventory, MIT license, and this implementation record. |

**MVVM is partial:** file-creation state uses `INotifyPropertyChanged` and a dedicated view model; the settings window still uses WinUI event handlers. A settings view-model/command refactor remains if consistent MVVM throughout is required. No unnecessary framework was added just to supply commands or dependency injection.

The mixed solution contains all projects. Use the build script to combine .NET CLI builds with Visual Studio MSBuild for C++; `dotnet build` alone cannot compile the native project.

## 9. Performance and security

Implemented:

- No continuously running service, scheduled background task, or startup registration.
- Explorer's command launches the UI without waiting for file creation.
- Fast native state queries avoid folder resolution.
- UI creation runs filesystem opening/checks off the dispatcher, including work that can block before an asynchronous write starts.
- Asynchronous writes, disposable file streams, COM smart pointers, and native process-handle cleanup.
- Removed unused AI/ML SDK payloads by referencing the required WinUI components individually.
- Atomic no-overwrite behavior and a filename-only boundary that rejects traversal and alternate streams.
- Literal template content with size limits; no execution or command expansion.
- Native process launch avoids shell command interpretation; text opening uses `ProcessStartInfo.ArgumentList`.
- Ordinary creation does not request elevation; classic registration uses HKCU.
- Native compiler warnings treated as errors, control-flow guard, ASLR, and DEP settings.

Pending validation:

- Cold/warm launch timings, memory use, repeated invocation, and resource-leak measurements.
- Explorer responsiveness on offline/slow network shares.
- Security review and adversarial filesystem/input testing beyond the existing unit suite.
- Full-disk, read-only-volume, long-path, reparse-point, and cancellation/partial-write scenarios.
- Verify no process remains after all app windows close across all supported environments.

The built unsigned framework-dependent MSIX is approximately 10.8 MB. This is an artifact size observation, not a complete performance benchmark.

## 10. Packaging, distribution, and automation

| Requirement | Completed | Pending |
| --- | --- | --- |
| Application icon and package assets | Original Mint SVG logo (mint-green page with white leaf) and reproducible PNG/multi-size ICO generation script. | Branding review if desired. |
| MSIX manifest | Name/version/publisher metadata, architecture, runtime dependency, `runFullTrust`, Explorer verbs, COM surrogate. | Production identity/publisher and deployment verification. |
| Build automation | Tool discovery, core tests, CLI/native/app builds, architecture switch, package staging. | Self-contained .NET distribution build not verified locally; verified build uses `-FrameworkDependent`. |
| Package validation | MakeAppx schema/payload validation enabled and passed. | Signing and trusted install. |
| Signing | Certificate-thumbprint signing and verification script. | A real signing certificate, matching Publisher, trust, and timestamp configuration. No key was created or committed. |
| Install/uninstall | MSIX install with optional dependencies; package/classic cleanup script. | Successful install, upgrade, repair, and uninstall verification. |
| GitHub Releases | Version metadata, license, unsigned MSIX, checksum, CI artifact upload configuration. Repository: https://github.com/workingpayload/Mint; CI passes. | Actual signed release publication. |
| Microsoft Store | MSIX foundation provided. | Store identity/association, restricted-capability review, WACK, certification, and submission. |
| Traditional installer | Unpackaged app and classic-menu scripts available. | No traditional installer or sparse/package-identity installer implemented. |
| Windows 10 / Windows 11 | Minimum API target and both menu implementations provided. | Real OS compatibility matrix, including modern Windows 11 placement. |
| ARM64 | Project and CI configuration provided. The ARM64 build passes in GitHub CI. | Build and test on ARM64 hardware. |

No machine-specific development path is hardcoded into application logic or build scripts. Local artifact paths in this document describe the current workspace outputs.

## 11. Build and test evidence

These are results from the implementation session, not fresh test executions performed merely to write this document.

| Check | Result |
| --- | --- |
| Core / CLI Release build | Passed. |
| WinUI x64 Debug and Release build | Passed; application launched. |
| Latest WinUI Release publish | Passed to `artifacts/app/x64/Final`. |
| Native x64 DLL | Passed. |
| Native COM factory/title/state/selection/lifetime smoke test | Passed. |
| Core automated suite | **50 passed, 0 failed, 1 skipped.** |
| Windows ACL permission test | Skipped because the sandbox denied changing ACLs. Must run outside the sandbox. |
| Actual WinUI creation | Unicode-named empty file created using Enter; automatic close confirmed. |
| Invalid-name UI behavior | `CON.txt` produced an inline error and disabled Create. |
| Escape / settings window | Escape close and dedicated settings-window launch inspected. |
| MSIX MakeAppx validation | Passed; unsigned package produced. |
| PowerShell script syntax | Passed. |
| Classic registration | Sandboxed attempts denied; verified in a normal session on Windows 11 26300 (see P0). |
| Modern package registration | Sandboxed attempts `0x80070005`; verified in a normal session on Windows 11 26300 (see P0). |
| Registered COM activation / modern menu visibility | Verified live, after fixing the bootstrapper exit under inherited package identity. |
| ARM64 build | Blocked by missing v143 ARM64 tools (MSB8020). |
| GitHub Actions | Verified 2026-10-14: workflow run [38471813367](https://github.com/workingpayload/Mint/actions/runs/38471813367) passed for x64 and ARM64 builds on windows-2025, with x64 native tests. The C++ projects use `$(DefaultPlatformToolset)` because the hosted runner lacks v143 ARM64 tools. |

Automated coverage includes normal/custom extensions, extensionless names, dotfiles, Unicode, invalid characters and device names, duplicate names, folder collisions, concurrent creators, nonexistent/relative directories, literal template content, size rejection, all built-in presets/templates, import/export collisions, malformed imports, configuration round trips, corrupt/future configuration preservation, and rejection of invalid saves.

The originally attempted xUnit restore was blocked by NuGet connectivity. The replacement runner is dependency-free, creates a fresh fixture per case, reports skips explicitly, and exits nonzero on failure. Run it with `dotnet run`; it is not a `dotnet test` adapter.

## 12. Problems found and fixed during implementation

- Replaced floating package versions with pinned installed package versions.
- Fixed native compilation warnings rather than disabling warning enforcement.
- Corrected the package verb ID to satisfy the MSIX schema's allowed characters.
- Corrected package asset paths and the expected WinUI PRI filename so MakeAppx validation passes without bypasses.
- Fixed unsupported configuration versions throwing instead of preserving the file and returning defaults; added regression coverage.
- Added DPI-aware sizing after inspecting the initial dialog layout.
- Moved validation feedback next to the filename field and ensured keyboard handling can observe handled key events.
- Added direct target-folder entry after the picker broker proved unreliable for UI automation.
- Moved potentially blocking filesystem setup off the UI dispatcher.
- Kept DLL thread notifications enabled for the static CRT.
- Fixed solution configuration mappings and verified that the solution lists all projects.
- Published the latest app to `Final` after an earlier test process locked the `Release` copy. This was a publishing/file-lock issue, not a compiler error.

## 13. Prioritized pending work

### P0: prove the main Explorer workflow

P0 work has started. The new [P0 verification guide](docs/P0-VERIFICATION.md) and `scripts/Invoke-P0Verification.ps1` provide a repeatable, evidence-producing workflow for an unrestricted user session. The agent session still cannot register the package, write the classic registry keys, or change test ACLs. Developer Mode was inspected and is already enabled; no security setting was changed.

Completed P0 preparation and local checks:

- Extracted Windows argument quoting into a shared native header and verified eight round trips through `CommandLineToArgvW`, including drive roots, UNC syntax, Unicode, spaces, percent signs, shell metacharacters, embedded quotes, and trailing backslashes.
- Expanded the native smoke test to reject missing sites, file selections, virtual folders, and multiple selections; negative invocation paths also fail instead of choosing an arbitrary folder.
- Verified filesystem and desktop background resolution through simulated Explorer service-provider sites. Real Explorer background invocation was later verified live.
- Exercised real local shell-item state queries for a drive root and a directory containing spaces, Unicode, ampersands, percent signs, and parentheses.
- Added optional `-UncDirectory` testing; no real UNC share was supplied, so only UNC argument syntax has been verified.
- Added `--filter` and `--require-no-skips` to the core test runner. The full local result remains 50 passed, 0 failed, 1 skipped; strict P0 mode correctly returns failure for the ACL skip.
- Added structured P0 reports with OS/architecture, application and DLL hashes, registration checks, registered COM activation, and explicit pending manual verification.
- Rebuilt the native DLL and unsigned MSIX, and verified that the staged DLL matches the tested native build.
- Repeated the registration attempts; both remain access-denied. No live-menu P0 item below is marked complete on the strength of simulated tests.

Live verification (2026-10-12, normal user session, Windows 11 26300 x64, real Explorer driven via UI Automation):

- Fixed: Invoke from the packaged surrogate started `CreateFile.App.exe` with package identity, and the WinAppSDK auto-bootstrapper exited with `0x80070032`. Added `WindowsAppSDKBootstrapAutoInitializeOptions_OnPackageIdentity_NoOp` and `_OnNoMatch_ShowUI` to `CreateFile.App.csproj`.
- Fixed: settings saved from an Explorer-launched dialog were virtualized into the package's LocalCache, so classic/CLI launches never saw them and uninstall deleted them. `packaging/AppxManifest.xml` and `src/CreateFile.App/Package.appxmanifest` now set `desktop6:FileSystemWriteVirtualization` to disabled and declare `unvirtualizedResources`.
- Fixed: classic launches displayed the target with a trailing `\.`. `App.xaml.cs` now resolves the `--folder` argument for display.
- Results and evidence paths are in [P0 verification](docs/P0-VERIFICATION.md#observed-results-windows-11-build-26300-x64).

- [x] Register the package on an unrestricted Windows 11 test machine.
- [x] Pass registered COM activation, and an Invoke that actually shows the dialog.
- [x] Verify modern menu placement for folders, folder backgrounds, and desktop; confirm correct target directories.
- [ ] Install and verify classic verbs on Windows 10 and Windows 11 Show more options. Windows 11 is done; Windows 10 is pending.
- [ ] Test redirected Desktop, drive roots, UNC paths, special characters, virtual locations, and multiple selections. All done except redirected Desktop and UNC.
- [x] Verify automatic Explorer refresh, duplicate-command behavior, repair, and uninstall cleanup. Classic shows a duplicate in Show more options; this is documented and not suppressed.
- [x] Run the ACL-denial test successfully outside the sandbox.

### P1: close feature and release-quality gaps

- [ ] Test all settings/preset/template UI flows and persistence after relaunch.
- [ ] Verify folder and template pickers manually.
- [ ] Add system toasts if notifications must remain visible after automatic close.
- [ ] Refactor settings to MVVM if the architecture must be consistent throughout.
- [ ] Complete Narrator, tab-order, high-contrast, theme-change, DPI/multi-monitor, small-display, and reduced-motion checks.
- [ ] Decide whether custom subtle transitions are needed beyond standard WinUI behavior.
- [ ] Measure startup/memory and test network/disk-failure behavior.
- [ ] Install ARM64 tools and validate ARM64 binaries on a real device.
- [ ] Verify self-contained .NET distribution builds and runtime dependency deployment.
- [ ] Sign a release package; exercise standard-user install, update, rollback expectations, and uninstall.

### P2: distribution and optional work

- [x] Run the supplied workflow in the real GitHub repository (passes x64 and ARM64). Publish releases when authorized.
- [ ] Complete Microsoft Store association, identity, certification, and submission.
- [ ] Add a traditional installer with supported package identity if that optional distribution route is needed.
- [ ] Consider richer update checks, automatic duplicate detection, configuration merge/backup, and a standard test adapter as follow-up enhancements.
- [x] Configure repository/update links: https://github.com/workingpayload/Mint.

## 14. Commands and deliverables

From the repository root:

```powershell
# Latest locally verified application from this session
& ./artifacts/app/x64/Final/CreateFile.App.exe --folder (Get-Location).Path
& ./artifacts/app/x64/Final/CreateFile.App.exe --settings

# Rebuild after closing any running staged app
./scripts/Build.ps1 -Architecture x64 -FrameworkDependent -Package

# Automated tests
dotnet run --project tests/CreateFile.Core.Tests -c Release
./scripts/Test-Shell.ps1

# Integration diagnostics; registration is still pending
./scripts/Verify-Integration.ps1

# P0 gate: run in a normal user PowerShell, outside the restricted agent session
./scripts/Invoke-P0Verification.ps1 -Stage ./artifacts/app/x64/Final -RegisterModern

# Isolate the permission-denial test; a skipped test must not count as a pass
dotnet run --project tests/CreateFile.Core.Tests -c Release -- --filter ReadOnlyFolder --require-no-skips
```

Current outputs:

- `artifacts/app/x64/Final/CreateFile.App.exe`: latest verified framework-dependent app; requires .NET 10 and the Windows App Runtime.
- `artifacts/CreateFile-0.1.0.0-x64.msix`: unsigned, SDK-validated package built from the latest code.
- `artifacts/SHA256SUMS.txt`: package checksum.
- `artifacts/shell/x64/Release/CreateFile.Shell.dll` and `ShellSmoke.exe`.
- `src/CreateFile.Cli/bin/Release/net10.0/CreateFile.Cli.exe`.
- `artifacts/ui-smoke/ui-created-验证.env`: UI test evidence.

Related documentation:

- [README: launch, classic/modern registration, signing, installation, removal, and troubleshooting](README.md)
- [Verification evidence and full Windows 10/11 manual checklist](docs/VERIFICATION.md)
- [Created, modified, and removed source-file inventory](docs/FILES.md)
- [MIT license](LICENSE)
