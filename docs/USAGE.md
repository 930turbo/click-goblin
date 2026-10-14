# Click Goblin usage

Open ClickGoblin.exe (or dist\ClickGoblin.exe when building from source). Choose a mouse button and either Clicks Per Second or an
interval. Start / stop with F6, speed up with F8, slow down with F7,
or stop immediately with F10. Use Hotkeys to record your own shortcuts
and set how much each speed adjustment changes the Clicks Per Second.

Hotkeys accept F1-F24 (with optional modifiers), or Ctrl / Alt plus a
letter, number, arrow up/down, or plus/minus. All four must be different
and available. Click a hotkey box and press the combination you want.
Editing temporarily unregisters shortcuts; leaving the box applies them.
Hotkey editing is disabled during a session. Avoid game key bindings.

## Click settings
- Clicks Per Second is a target up to 500. Actual throughput depends on Windows and
  the app. The worker sleeps between events; it does not spin at 100% CPU.
- Interval and Clicks Per Second are linked. You can adjust speed while running.
- Hold defaults to 50 ms for games. Actual hold is capped at half the
  interval, leaving the other half for the button to be released.
- Left, right, middle and both side buttons are supported.

## Sessions
- Start delay gives you time to switch apps. First click happens after
  the countdown plus one normal interval. Press start/stop to cancel.
- Set a click limit or a time limit to stop automatically. 0 is unlimited.
  If both are set, the first limit reached stops the session.
- Time limit starts after the countdown, and includes app-lock pauses.
- Optional app lock captures the first foreground app after starting,
  excluding Click Goblin. It pauses in other apps and resumes when you
  return. If you start from the app's button with app lock enabled, you
  get at least 3 seconds to switch into the intended app.
- Minimize keeps it working. Closing stops clicking and releases input.
- Every session starts fresh; the program always opens stopped.

Some programs require Click Goblin to run as administrator, especially
when the receiving program is also elevated. Right-click Click Goblin and
choose **Run as administrator** if input is blocked. Compatibility with
simulated input depends on the receiving program.

Preferences: %LOCALAPPDATA%\ClickGoblin\settings.xml
Earlier LightClick timing settings are imported on first launch.
Keep on top affects only the window. It does not change input targeting.
Without app lock, clicking follows your foreground app and mouse pointer.
There are no network connections, installers, startup entries or services.

To remove, close the app and delete its files and desktop shortcut.
You can optionally delete the preferences folder too.

## Goblin Gang / Multi-Clicker

Enable Goblin Gang to click several mouse buttons with one hotkey. Select
Left, Right, Middle, Side X1, or Side X2 in any combination. Turn it off
to return to the single-button selector on Clicking.

- **Together:** all selected buttons are pressed in one Windows input
  batch, then released together after the effective hold time.
- **Staggered:** button presses are spread evenly over each interval,
  in Left / Right / Middle / Side X1 / Side X2 order.
- Clicks Per Second applies to each selected button. The click limit
  counts completed groups, rather than individual button presses.
- Hold time is capped at half the interval in Together mode, or half
  each button's time slot in Staggered mode, with a minimum of 1 ms.
- Very fast multi-button patterns may run slower than the requested
  speed. Inputs never try to catch up by emitting bursts after a stall.
- App lock, time limits and emergency stop cover the whole group.
  Stopping also releases the currently held button or group.

## Settings

Choose Lilac, Mint, Rose, Sky or Amber accents with Midnight, Charcoal or
Deep blue backgrounds. Colors update immediately and are saved.

Optionally minimize to the notification area. Double-click the goblin
icon to reopen, or use its menu to start, stop or quit. Hotkeys remain
active while hidden; closing the window always quits and releases input.
