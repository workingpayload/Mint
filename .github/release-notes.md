Mint adds **Create New File** to the Explorer right-click menu: pick a file type (or search for one), name it, and create it in that folder.

## Install

1. Download the installer for your PC:
   - `Mint-Setup-<version>-x64.exe` for most Intel/AMD PCs
   - `Mint-Setup-<version>-ARM64.exe` for ARM PCs (Snapdragon / Surface Pro X)
2. Run it. No administrator rights are needed; it installs for your user only.
   The installer is not code-signed yet, so Windows SmartScreen may warn: choose **More info → Run anyway**.
3. Right-click a folder, a folder background, or the desktop and choose **Create New File**.
   On Windows 11 it is under **Show more options** (or press Shift+F10).

Everything is bundled (.NET and Windows App SDK); nothing else to install.
Uninstall from **Settings → Apps → Installed apps → Mint**. Your settings in `%LOCALAPPDATA%\CreateFile` are kept.

Verify the download with `SHA256SUMS.txt`: `Get-FileHash .\Mint-Setup-*.exe -Algorithm SHA256`.

## Preview limitations

- The top-level Windows 11 menu entry needs a signed MSIX and is not part of this installer.
- Tested on Windows 11 x64. Windows 10, ARM64 hardware, OneDrive-redirected Desktop, and network shares are not verified yet.

Report problems at https://github.com/workingpayload/Mint/issues.
