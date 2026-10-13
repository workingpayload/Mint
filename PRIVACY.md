# Privacy Policy

**Mint for Explorer**. Effective date: October 14, 2026.

Mint for Explorer ("the app") is an open-source Windows utility that adds **Create New File** to File Explorer. This policy explains what information the app handles. In short: **the app does not collect, transmit, sell, or share any personal data.**

## Information we collect

None. The app has:

- no telemetry, analytics, or usage tracking;
- no crash reporting or diagnostics upload;
- no advertising or advertising identifiers;
- no user accounts or sign-in;
- no background service or startup task.

The app never connects to the internet on its own. Its code makes no network requests.

## Information stored on your device

The app stores the following only on your PC, and it never leaves your PC:

| What | Where | Why |
| --- | --- | --- |
| Settings: theme, default filename, close/open-after-creation options, notification preference, file-type presets, and text templates | `%LOCALAPPDATA%\CreateFile\settings.json` | Remember your preferences |
| Explorer menu entries (only if you enable the classic menu) | `HKEY_CURRENT_USER\Software\Classes\...\shell\CreateFile.New` | Show **Create New File** in the right-click menu |

The app does not record which folders you open, which files you create, or what you type. Files you create are written only to the folder you choose.

Uninstalling with the setup program removes the program files and the menu entries it created. In the Microsoft Store/MSIX version, if you turned on the classic menu in Settings, turn it off there before uninstalling. Your `settings.json` file is kept so that a reinstall keeps your presets. Delete the `%LOCALAPPDATA%\CreateFile` folder to remove it.

## Files you choose to import or export

When you import or export a template, the app reads or writes only the file you pick in the Windows file dialog.

## Optional actions that leave the app

- **Open in Notepad**: if you turn this on, the app starts Windows Notepad (`notepad.exe`) with the file you just created.
- **GitHub repository / Check for updates**: these buttons open https://github.com/workingpayload/Mint in your web browser. The app does not download or install updates itself. GitHub's [privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement) applies to your visit.

## Third parties

The app includes no third-party SDKs that collect data. Other services you may use around the app have their own policies, for example:

- downloading the installer from GitHub Releases ([GitHub privacy statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement));
- installing from the Microsoft Store, and Windows features such as SmartScreen ([Microsoft privacy statement](https://privacy.microsoft.com/privacystatement)).

## Children's privacy

The app collects no data from anyone, including children under 13.

## Changes to this policy

If this policy changes, the updated version will be published in this file with a new effective date. You can see the full history of the file in the repository.

## Contact

Ask questions or report privacy concerns by opening an issue at https://github.com/workingpayload/Mint/issues.
