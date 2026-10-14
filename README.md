# Click Goblin

Tiny, portable Windows auto-clicker with a dark UI. Run [ClickGoblin.exe](dist/ClickGoblin.exe?raw=true). [Usage](USAGE.txt).

- Set CPS (up to 500 requested) or an interval; speed changes work live.
- Default hotkeys: **F6** start/stop, **F8** faster, **F7** slower, **F10** emergency stop. Change them in Hotkeys.
- Mouse buttons, hold time, countdown, click/time limits, and optional app lock that pauses on Alt-Tab.

Starts stopped. Settings are saved locally. Actual CPS depends on Windows and the target app. For an elevated game, run the app as administrator too.

Build: `powershell -File .\Build.ps1` (Windows .NET Framework). Tests: `powershell -File .\Test.ps1`.
