# Development

Requires Windows and the .NET Framework C# compiler included with .NET Framework 4.8. No NuGet packages or additional runtime downloads. Outfit is embedded under the SIL Open Font License; see [third-party notices](../THIRD_PARTY_NOTICES.md).

From the repository root:

```powershell
.\scripts\build.ps1       # dist/ClickGoblin.exe
.\scripts\test.ps1        # engine tests and desktop UI checks
.\scripts\package.ps1     # portable ZIP and SHA-256 checksums
```

CI runs `scripts/test.ps1 -SkipUi` on Windows. Desktop checks additionally exercise hotkey registration, input synchronization, and closing during a countdown. Tests use simulated input and never click the real mouse.

| Folder | Contents |
| --- | --- |
| `src/ClickGoblin/` | Application, input engine, preferences, UI controls |
| `assets/` | Application icon and Windows manifest |
| `scripts/` | Build, test, and packaging scripts |
| `tests/` | Dependency-free test runner |
| `docs/` | Usage guide and app screenshot |
| `dist/` | Current runnable build |

Release downloads include the EXE, a portable ZIP, and SHA-256 checksums. The executable is unsigned. Build from source if you prefer to verify your own binary.

UI checks cover custom picker keyboard selection, numeric validation, contrast across all palettes, close-to-tray, explicit Quit, and hotkey filter installation. Core checks cover repeat suppression and shortcut pass-through. Test shortcuts use simulated matcher events; compatibility with third-party raw-input applications is not asserted.
