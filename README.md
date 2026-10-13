# Mint

Mint (project/code name: CreateFile) is a native Windows utility for creating any valid filename from Explorer. C# / .NET 10, WinUI 3, and a small native C++ `IExplorerCommand` shell extension. MIT licensed. No service, startup task, telemetry, Electron, or browser-based UI.

The product display name is **Mint**. Internal identifiers such as the package identity, project, executable, and namespace names, and the `%LOCALAPPDATA%\CreateFile` settings folder still use `CreateFile`. This keeps existing registrations, scripts, and saved settings working.

**Status: first working implementation, not a verified production release.** The x64 app, CLI, native DLL, automated tests, and unsigned MSIX build locally. The SDK validates the MSIX manifest. The modern and classic Explorer menus have been verified live on Windows 11 build 26300 x64. Windows 10, ARM64, redirected Desktop and UNC shares are still unverified. See [verification results and checklist](docs/VERIFICATION.md).

## Install (users)

Download `Mint-Setup-<version>-x64.exe` (or `-ARM64.exe`) from [Releases](https://github.com/workingpayload/Mint/releases) and run it. It installs per user into `%LOCALAPPDATA%\Programs\Mint` without administrator rights, bundles .NET and the Windows App SDK, adds Start menu shortcuts (**Mint**, **Mint Settings**), and optionally adds **Create New File** to the classic Explorer menu (under **Show more options** on Windows 11). The installer is unsigned for now, so SmartScreen may ask you to choose **More info → Run anyway**. Uninstall it from Installed apps. The uninstaller removes only menu entries that point to this installation and keeps settings. The top-level Windows 11 menu still needs the signed MSIX below.

Follow the [P0 runbook](docs/P0-VERIFICATION.md) for strict tests, registration, registered COM activation, and the required Explorer observations. The runner saves structured evidence under `artifacts/p0` and never treats simulated native tests as proof of menu placement. The package declares the restricted `unvirtualizedResources` capability so that settings saved from an Explorer launch go to the real `%LOCALAPPDATA%\CreateFile`, which is shared with classic and CLI launches and kept on uninstall.

## Features

- Arbitrary extensions, extensionless filenames, dotfiles, and Unicode names.
- Atomic `FileMode.CreateNew`: existing files are never silently overwritten, even during concurrent creation.
- Windows filename/device-name validation; no path traversal or alternate data streams through the filename field.
- Empty files or literal UTF-8 templates. A zero-byte `.pdf`, `.docx`, `.png`, or `.zip` is explicitly described as an empty file, not a valid structured document.
- Fluent WinUI creation dialog: Mica, system/light/dark themes, inline errors, keyboard shortcuts, accessible names, folder picker, preset search, favorites, and template selection.
- 22 requested language/text presets plus `.env`, `.gitignore`, and `Dockerfile`; eight starter templates.
- Settings: general preferences, preset CRUD/pinning/order, template CRUD/JSON import/export, classic integration enable/repair/removal, and package status.
- Optional opening in **Notepad**, never through an executable/script association. Success feedback is an in-dialog InfoBar (not a system toast).
- Classic per-user verbs and modern package-declared COM integration both launch the same creation dialog.

## Requirements

- Windows 10 1809+ APIs are targeted; Windows 11 is required for the modern menu. Both OS families still require manual release testing.
- .NET 10 SDK to build; Visual Studio with MSVC v143, Windows SDK 10.0.26100+, and the matching x64/ARM64 compiler components.
- NuGet access for an initial restore. Versions are pinned in the project files.
- Windows App Runtime **2.5.1** for execution. WinUI components are referenced individually to avoid the SDK's unused AI/ML payloads. WinUI transitively ships WebView2 interop; CreateFile never instantiates a browser.
- The local `-FrameworkDependent` build also requires the .NET 10 runtime. Default distribution builds bundle .NET, but still require the Windows App Runtime.

## Build and test

From the repository root in PowerShell:

```powershell
# Verified local build mode; produces an unsigned MSIX as well.
./scripts/Build.ps1 -Architecture x64 -FrameworkDependent -Package

# Distribution build: bundles .NET and needs its runtime pack from NuGet.
./scripts/Build.ps1 -Architecture x64 -Package

# Requires the ARM64 MSVC tools. Run ARM64 binaries on an ARM64 Windows device.
./scripts/Build.ps1 -Architecture ARM64 -Package

# Dependency-free automated core test runner (not a dotnet-test adapter).
dotnet run --project tests/CreateFile.Core.Tests -c Release
./scripts/Test-Shell.ps1

# Per-user installer (needs Inno Setup 6): artifacts/Mint-Setup-<version>-<arch>.exe
./scripts/Build-Installer.ps1 -Architecture x64
```

Pushing a tag `v<version>` that matches `Directory.Build.props` runs `.github/workflows/release.yml`. It runs the core tests, builds both installers, and publishes a GitHub Release with `SHA256SUMS.txt` and the notes in `.github/release-notes.md` as the Latest release.

`Build.ps1` discovers Visual Studio and the SDK; no machine-specific paths are embedded. It rebuilds its own `artifacts/app/<architecture>/<configuration>` staging directory. Close a running staged app before rebuilding. Use this script for the mixed C++/.NET solution; `dotnet build CreateFile.slnx` alone cannot build native VC projects. The solution is also provided for an appropriately equipped Visual Studio version with .NET 10 support.

The offline runner prints each test, failure, and skip and exits nonzero on failure. It was chosen because the sandbox could not download xUnit. The ACL test reports a skip if the host prohibits ACL changes; a skip is not a pass. Native smoke tests validate COM exports, factory creation, title, flags, folder state, and DLL lifetime; they do not prove Explorer registration or background-site behavior.

## Launch

The latest verified binary from this development session is in `artifacts/app/x64/Final` (an earlier test process kept `Release` locked). Launch it now with:

```powershell
& ./artifacts/app/x64/Final/CreateFile.App.exe --folder (Get-Location).Path
& ./artifacts/app/x64/Final/CreateFile.App.exe --settings
```

After the build above:

```powershell
& ./artifacts/app/x64/Release/CreateFile.App.exe --folder (Get-Location).Path
& ./artifacts/app/x64/Release/CreateFile.App.exe --settings

# Direct development build and launch:
dotnet build src/CreateFile.App -c Debug -p:Platform=x64
& ./src/CreateFile.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/CreateFile.App.exe --folder (Get-Location).Path

# CLI: arguments are literal, with no command interpreter.
dotnet run --project src/CreateFile.Cli -- create (Get-Location).Path main.py
dotnet run --project src/CreateFile.Cli -- validate .gitignore
dotnet run --project src/CreateFile.Cli -- presets
dotnet run --project src/CreateFile.Cli -- create (Get-Location).Path index.html --template ./my-template.json
```

Without arguments the app targets the user's actual Desktop directory. Enter in the filename field creates; Escape cancels. The target folder can be entered directly or chosen through the native folder picker. Choose a preset to apply its default filename. Empty file is always available. Creation failures remain in the dialog and never request elevation.

## Classic Explorer integration

Keep the unpackaged application in a **stable installation folder**, or use the staged build only for development:

```powershell
./scripts/Classic-Integration.ps1 -ExecutablePath ./artifacts/app/x64/Release/CreateFile.App.exe
./scripts/Verify-Integration.ps1
```

1. Open a new Explorer window to a writable test folder.
2. Right-click its background, a folder object, and the desktop. On Windows 11 choose **Show more options**.
3. Select **Create New File** and verify the target displayed in the dialog.
4. Create `main.py`, `.env`, and `Dockerfile`; confirm each appears without a manual refresh. Retry the same filename and confirm rejection.
5. Test a path with spaces, Unicode, `&`, `%`, and a drive root. The registry command appends `\.` inside the quoted target to handle trailing backslashes safely.
6. Remove and verify the command disappears:

```powershell
./scripts/Classic-Integration.ps1 -Remove
```

Keys are confined to `HKCU\Software\Classes\{Directory\Background,Directory,DesktopBackground}\shell\CreateFile.New`. No administrator rights are requested. Settings offers the same actions. Classic installation is opt-in: packaged commands may also surface in the classic menu, so test the packaged command before enabling a second classic verb. Repair after moving the executable or updating a versioned package path. A script installation should be removed with the script; package uninstall alone cannot clean independently created registry entries.

## Modern Windows 11 integration

The DLL implements `IExplorerCommand` and `IObjectWithSite`. `packaging/AppxManifest.xml` declares `desktop4:FileExplorerContextMenus`, `desktop5:ItemType` for `Directory` and `Directory\Background`, and `com:SurrogateServer`. The desktop's virtual root is resolved to its known filesystem folder in native code. No unsupported modern-menu registry override is used.

For local development **on a machine where Developer Mode is already enabled**:

```powershell
./scripts/Build.ps1 -Architecture x64 -FrameworkDependent -Package
Add-AppxPackage -Register (Resolve-Path ./artifacts/app/x64/Release/AppxManifest.xml).Path
./scripts/Verify-Integration.ps1
./scripts/Test-Shell.ps1
& ./artifacts/shell/x64/Release/ShellSmoke.exe (Resolve-Path ./artifacts/app/x64/Release/CreateFile.Shell.dll).Path --registered
```

Do not move or clean a loose registered package's staging directory before unregistering it. Packaged COM activation through `--registered` must pass; the regular native smoke test is not a substitute.

For a signed MSIX installation:

```powershell
# Use a code-signing certificate already provisioned and trusted on the test machine.
# Its subject must match CN=CreateFile, or rebuild with your actual Publisher:
./scripts/Package.ps1 -Stage ./artifacts/app/x64/Release -Publisher 'CN=Your Publisher'
./scripts/Sign.ps1 -PackagePath ./artifacts/CreateFile-0.1.0.0-x64.msix -CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT
./scripts/Install.ps1 -PackagePath ./artifacts/CreateFile-0.1.0.0-x64.msix
./scripts/Verify-Integration.ps1
```

`Install.ps1` accepts `-DependencyPath` for runtime MSIX dependencies. It does not change certificate trust, enable Developer Mode, elevate, or restart Explorer. Signing identity and trust are release-owner responsibilities; the included MSIX is unsigned and is not directly installable as a trusted release.

After registration, open a new Explorer window and verify **Create New File directly in the Windows 11 menu** for a folder, a background, and the desktop. Test correct target resolution and file creation for all three. If Explorer caches an old handler, sign out/in after saving work; the scripts never forcibly kill Explorer. Full checks are in [VERIFICATION.md](docs/VERIFICATION.md).

```powershell
# Removes the registered package and this application's classic entries, retaining settings.
./scripts/Uninstall.ps1
```

## Configuration and templates

Configuration is JSON under `Environment.SpecialFolder.LocalApplicationData/CreateFile/settings.json` (packaged Windows redirection may change its physical location). Saves use a unique temporary file and atomic rename. Invalid or future-version files are preserved on load; defaults and a warning are shown. Explicitly saving settings replaces that file. Simultaneous independent settings windows use last-successful-save semantics; there is no cross-process merge.

Template import/export uses this JSON shape, never code evaluation:

```json
{"Id":"my-template","Name":"My template","Content":"Hello, world!\n"}
```

Imports receive fresh IDs. Text is limited to 1 MiB UTF-8, configuration/import JSON to 8 MiB. Template exports in the CLI/core refuse collisions; the UI save picker asks before replacing a chosen export file. Templates are literal text without token expansion or binary-format generation.

## Layout

| Area | Responsibility |
| --- | --- |
| `src/CreateFile.Core` | Filename validation, asynchronous creation, built-ins, JSON settings/templates |
| `src/CreateFile.App` | WinUI windows, creation view model, activation, theme, classic registration |
| `src/CreateFile.Cli` | `create`, `validate`, `presets`; predictable exit codes |
| `src/CreateFile.Shell` | Native COM DLL and independent smoke-test executable |
| `tests/CreateFile.Core.Tests` | Offline automated test suite |
| `packaging`, `scripts`, `assets` | Manifest, build/sign/install/diagnostics, reproducible icon source |
| `installer/Mint.iss` | Per-user Inno Setup installer (self-contained app, classic verb) |
| `.github/workflows/build.yml` | x64/ARM64 CI, tests, unsigned build artifacts |
| `.github/workflows/release.yml` | Tag-triggered installers and GitHub Release |

## Release readiness and limitations

- Run the Windows 10/11 manual matrix and registered COM activation before claiming Explorer support. Registration was denied in this sandbox.
- ARM64 builds pass in GitHub CI, but ARM64 execution on hardware is untested.
- System toast notifications are not implemented; success feedback is in the dialog and is not visible after automatic close.
- About/update links open https://github.com/workingpayload/Mint (`ProjectInfo.RepositoryUrl`). Updates mean opening the GitHub Releases page; Mint does not download or install code automatically.
- Store submission needs the Store-assigned identity/publisher, signing, `runFullTrust` review, Windows App Certification Kit testing, and Store association. These are not provided by an unsigned local package.
- Two distribution paths are supplied. The unsigned per-user Inno Setup installer gives the classic menu only. The MSIX also gives the top-level Windows 11 menu, but needs signing and trust. A sparse identity package is not implemented. Neither the installer nor the MSIX is code-signed yet.
- Windows may reject long paths depending on filesystem, policy, and runtime. Permission errors, offline shares, and disk-full failures are reported. A failed/cancelled write may leave a partial **new** file; the app warns rather than deleting a path another process might have replaced.
- The settings window uses straightforward WinUI event handlers; file-creation state uses MVVM. Broader UI automation, accessibility/high-contrast and release performance testing remain on the manual checklist.

## Microsoft references

- [Package extension and COM registration](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions)
- [Explorer context-menu manifest schema](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop4-fileexplorercontextmenus)
- [Directory/background item types](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop5-itemtype)
- [IExplorerCommand GetState and the fast-query contract](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-iexplorercommand-getstate)
- [Windows App SDK release channels](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-channels)

Contributions are welcome under [MIT](LICENSE). Never commit signing keys, change the COM CLSID casually, disable MSIX schema validation, or replace atomic exclusive creation with an existence-check/write sequence.
