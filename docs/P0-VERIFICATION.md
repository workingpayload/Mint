# P0: verify the actual Explorer workflow

P0 remains open until both automated evidence and real Explorer observations pass. The native tests use real shell items but simulated background sites; they are not a substitute for Explorer.

## Observed results: Windows 11 build 26300 x64

Real Explorer was driven via UI Automation and mouse input at 125% DPI. Evidence: `artifacts/p0/manual/20261012-030831` (`*-results.json`, `classic-and-cleanup-results.txt`) and `artifacts/p0/20261012-041514-e70f66bd/report.json`.

Two defects found and fixed during this run:

- **Explorer command did not open the dialog.** The packaged COM surrogate's child process inherits package identity, and the WinAppSDK auto-bootstrapper exited with `0x80070032` (Application log, Windows App Runtime event 50). `CreateFile.App.csproj` now sets `WindowsAppSDKBootstrapAutoInitializeOptions_OnPackageIdentity_NoOp`, because the package already declares the runtime dependency. Unpackaged (classic) launches still bootstrap. Registered COM activation alone did not catch this; Invoke must show the dialog.
- **Settings saved from an Explorer-launched dialog were virtualized** into `%LOCALAPPDATA%\Packages\<family>\LocalCache`. That hid them from classic/CLI launches and deleted them on uninstall. The package manifest now disables file-system write virtualization (`desktop6:FileSystemWriteVirtualization` plus the restricted `unvirtualizedResources` capability).

Also: classic dialogs showed the target as `<folder>\.` (from `"%V\."`). The app now displays the resolved full path.

Classic results: with both integrations enabled, the Windows 11 menu still shows the command once, but Show more options shows it **twice** on folder background, folder object and desktop (packaged plus classic). This is expected, because duplicate suppression is not implemented. Repeating enable (repair) keeps the count at two. Both entries target correctly: special-character folder, `Plain`, `C:\` and Desktop. The classic entry runs without package identity. After `Classic-Integration.ps1 -Remove`, the count returns to one (15/15 checks pass). After `Uninstall.ps1`, the package and classic keys are absent, a fresh Explorer shows the command in neither menu, and `settings.json` is unchanged.

Still open: Windows 10 classic, redirected/OneDrive Desktop, real UNC share, ARM64. Explorer must also show the menu icon (`GetIcon` now returns the packaged `Assets\AppIcon.ico`); no one has checked this live yet.

## 1. Run the prepared checks outside the restricted agent session

Open a normal, non-administrator PowerShell in the repository root. The inspected development machine already has Developer Mode enabled; this workflow does not change security settings or certificate trust.

Close CreateFile dialogs first. Do not rebuild or move a loose package directory while it is registered. If a different CreateFile installation already exists, resolve it deliberately before registering the development stage.

```powershell
./scripts/Invoke-P0Verification.ps1 -Stage ./artifacts/app/x64/Final -RegisterModern
```

This runs strict core tests (any skip is a failure), native tests, checks staged DLL hashes, registers the package, validates its manifest, and probes registered out-of-process COM activation. It writes `artifacts/p0/<run>/report.json` and `registration.txt`. The classic check is expected to report absent on this first, modern-only pass. The report deliberately keeps `P0Complete` false: visual verification is still required.

If normal PowerShell reports access denied too, retain the full error and deployment ActivityId; do not treat the problem as solved or try unsupported registry overrides. If staged DLL hashes differ, rebuild/package before registration rather than replacing the registered binary.

## 2. Modern-only Explorer checks

Use a writable test folder under `artifacts/p0/manual`. Open a new Explorer window and record these separately:

| Case | Expected | Observed result / evidence |
| --- | --- | --- |
| Folder background | Create New File directly in the Windows 11 menu; dialog targets that folder. | Passed: count 1; correct target (also special-character folder). |
| Folder object | Command targets the selected folder, not its parent. | Passed: count 1; selected folder targeted. |
| Desktop background | Command targets the user's actual Desktop location. | Passed: count 1; target `C:\Users\<user>\Desktop` (not redirected). |
| Show more options before classic registration | Record whether packaged command already appears and its count. | Packaged command appears once (folder background and folder object). |
| Create `.env` with Enter | File appears without F5; dialog closes if enabled. | Passed: shown without F5; dialog closed; 0 bytes. |
| Retry `.env` | Collision error; original file unchanged. | Passed: inline collision error; content unchanged. |
| Spaces, Unicode, `&`, `%`, parentheses | Literal correct target; no argument splitting or expansion. | Passed: folder `Spaces & Uni ünï (100%) [x]`; file `a & b %PATH% (1) ünï.txt` created literally. |
| Drive root | Correct root displayed; ordinary permission failure is acceptable for an unwritable root. | Passed: `C:\` displayed. |
| Redirected/OneDrive Desktop | Correct redirected location. Requires a configured test environment. | Open: no redirected Desktop on the test machine. |
| Existing UNC test share | Correct path and refresh; also check an unavailable share remains responsive. | Open: no test share. |
| This PC / Recycle Bin / search results | No arbitrary fallback folder or inappropriate command. | Passed: command absent on all three. |
| Two selected folders | No arbitrary target or unintended multiple dialogs. | Passed: command absent. |

UNC quoting is tested automatically; UNC filesystem access is only tested when an explicit existing share is supplied:

```powershell
./scripts/Test-Shell.ps1 -UncDirectory '\\server\test-share'
```

Use a real test-share path in that command. Do not claim redirected Desktop or UNC behavior passed on a machine without those environments.

## 3. Classic enable, duplicate, and repair checks

After recording modern-only behavior:

```powershell
./scripts/Invoke-P0Verification.ps1 -Stage ./artifacts/app/x64/Final -EnableClassic
```

Verify folder background, folder object, and desktop through Windows 11 Show more options. Record command counts with both integrations enabled. If a duplicate appears, remove the optional classic integration on that machine and record the result; automatic duplicate suppression is not implemented.

Run the same enable command again to exercise idempotent repair and verify command counts do not grow. Verify a created file appears immediately and duplicate creation remains rejected. Repeat the classic cases on Windows 10; Windows 11 results cannot stand in for that test.

## 4. Removal and cleanup checks

Record observations before removing each integration:

```powershell
./scripts/Classic-Integration.ps1 -Remove
./scripts/Verify-Integration.ps1
```

The three classic keys must be absent. Open a fresh Explorer window; the packaged command may remain because the modern package is still installed.

For the deliberately installed development package:

```powershell
./scripts/Uninstall.ps1
./scripts/Verify-Integration.ps1
```

Confirm the package is absent, all three classic keys are absent, and neither menu contains the command. Preserve user settings. Do not remove an unrelated production installation to complete this checklist. Explorer may cache handlers; save work before signing out/in if needed. The scripts do not forcibly restart Explorer.

## 5. Evidence needed to close P0

- Strict core runner passes with **zero skipped tests**, including ACL denial.
- Native standalone tests and registered COM activation pass against matching DLL hashes.
- Completed observations above, with Windows build and architecture.
- Actual refresh, duplicate behavior, repair, and cleanup observations.
- Windows 10 classic and Windows 11 modern/classic evidence.
- Redirected Desktop and real UNC evidence, or those subcases remain explicitly open.

Return the generated `report.json` path/content and the observed menu results. No setup, metadata, or simulated-site test alone closes P0.
