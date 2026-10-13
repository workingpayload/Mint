# Verification record and release checklist

## Live Explorer evidence (2026-10-12, normal user session)

Windows 11 build 26300 x64, 125% DPI. The strict core suite passed (51 tests, 0 skipped, including ACL denial). Package registration, registered COM activation and native tests also passed. Real Explorer menus were driven via UI Automation. Full results and the two packaging fixes (bootstrapper under inherited package identity; AppData write virtualization) are in [P0-VERIFICATION.md](P0-VERIFICATION.md#observed-results-windows-11-build-26300-x64). This supersedes the access-denied registration notes below for this machine.

## Local evidence (2026-10-12)

- .NET SDK: 10.0.401; Windows host: build 26300, x64.
- Core and CLI: compiled; automated core suite **50 passed, 0 failed, 1 skipped**.
- ACL denial test: skipped because the sandbox refuses ACL changes. The test exists and must pass on an unrestricted Windows test machine. A read-only directory attribute alone is not a meaningful Windows write-permission test.
- Native x64 DLL: compiled with MSVC v143, warnings treated as errors. COM factory/title/state/selection/unload smoke test passed. Native `Invoke` successfully started an app process; this alone does not prove UI activation or Explorer placement.
- WinUI Debug/Release: compiled. Creation dialog and dedicated settings window launched and inspected through accessibility and screenshots. Reserved name `CON.txt` showed an inline message and disabled Create. Escape closed the dialog.
- Final Release end-to-end UI check: typed the workspace target folder and `ui-created-验证.env`, pressed Enter, verified the file exists with length 0, and verified automatic dialog close. Evidence file: `artifacts/ui-smoke/ui-created-验证.env`. Latest executable: `artifacts/app/x64/Final/CreateFile.App.exe`.
- The native folder picker opened, but the automation helper could not reliably target the broker dialog. Picker selection/import/export still need manual testing. An earlier `Release` test window may remain open; close it before rebuilding that staging directory.
- MSIX: MakeAppx schema and payload validation passed, unsigned package produced. No validation bypass used.
- Classic install attempt: denied when creating HKCU shell keys.
- Modern loose package registration attempt: `Add-AppxPackage` returned `0x80070005`, access denied. **Packaged COM activation and actual Explorer context menus remain unverified.**
- Retried both registration methods at the user's request; both remained denied. Follow-up diagnostics show no package and no classic entries installed.
- ARM64 native build: MSB8020, matching v143 ARM64 tools not installed. ARM64 execution untested.
- NuGet test-framework restore was blocked; the suite uses an explicit dependency-free runner. Use `dotnet run`, not `dotnet test`.

## Matrix to complete before release

For the P0 sequence and a structured evidence report, use [P0-VERIFICATION.md](P0-VERIFICATION.md). Native coverage now includes eight Windows argument round trips, invalid/multiple selection rejection, drive-root and special-character local paths, and simulated folder/desktop background sites. These passed locally. Actual Explorer background invocation and registered COM activation were later verified live on Windows 11 26300 x64; real UNC access remains unverified. Strict core-test mode treats the ACL skip as a failed verification gate.

Test Windows 10 22H2 and supported Windows 11 builds, on x64 and an ARM64 device. Record OS build, architecture, package version, signing identity, install mode, outcome, and screenshots.

### Deployment and Explorer

- [ ] Install signed MSIX as a standard user with runtime dependencies present.
- [x] `Verify-Integration.ps1` shows the correct package, DLL, item types, and CLSID.
- [ ] `ShellSmoke.exe <installed-dll-path> --registered` activates the packaged COM server.
- [x] Windows 11: command visible directly in modern menu for a folder object, folder background, desktop.
- [ ] Classic command on Windows 10 and Windows 11 Show more options for the same three cases.
- [ ] Correct target for local folders, redirected/OneDrive Desktop, drive roots, UNC shares, and Unicode/spaced names.
- [ ] Test target paths containing `&`, `%`, parentheses, and trailing backslashes; verify literal argument handling.
- [x] Non-filesystem locations (This PC, Recycle Bin, search collections) do not silently write to an unrelated folder.
- [x] Multiple selected folders do not choose an arbitrary target.
- [ ] No duplicate command with preferred integration enabled; disable optional classic verb if package already supplies it.
- [x] New file appears without manually refreshing Explorer.
- [ ] Explorer remains responsive while opening menus repeatedly, including slow/offline shares.
- [ ] Repair fixes stale classic path; package reinstall updates modern DLL registration.
- [x] Remove classic, uninstall package, verify no dangling entries; settings retained deliberately.

### Creation and safety

- [ ] `main.py`, `index.js`, `App.tsx`, `README.md`, `.env`, `.gitignore`, `Dockerfile`, Unicode, custom extension.
- [ ] Existing file and directory collision; verify original contents are unchanged.
- [ ] Concurrent creators: only one wins.
- [ ] Reserved names including `CON.txt`, `COM¹`, invalid separators, `..`, ADS colon, trailing dot/space.
- [ ] Nonexistent, permission-denied, read-only-volume, full-disk, and unavailable-share errors stay in dialog.
- [ ] Empty files are exactly zero bytes; text templates are literal UTF-8 with no BOM and never executed.
- [ ] Opening `.ps1`, `.cmd`, `.bat`, `.exe`, `.url` uses Notepad, not their executable association.
- [ ] No UAC prompt during normal creation or classic per-user setup.

### UI, settings, and accessibility

- [ ] Enter creates when valid; Escape cancels; keyboard focus and tab order are predictable.
- [ ] Narrator reads target, filename, invalid-state message, controls, and result.
- [ ] System, Light, Dark, High Contrast, 100/150/200% DPI, reduced motion, and small displays.
- [ ] Preset search, add/update/delete, pinning and reordering persist after relaunch.
- [ ] Template add/edit/delete/import/export; malformed and oversized imports rejected.
- [ ] Explicit empty-file option survives all template changes.
- [ ] Default filename, close-after-create, Notepad option, and success feedback persist.
- [ ] Corrupt/unsupported settings show a warning and preserve the source file until explicit save.
- [ ] Measure warm/cold launch latency, private bytes, package size; no resident process after all windows close.
- [ ] WACK and Store packaging/identity checks pass before Store submission.
