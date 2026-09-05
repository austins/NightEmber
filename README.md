# 🌙 Night Ember

Night Ember is a portable night-light application for Windows. It applies a warm color temperature directly through each
display's gamma ramp and runs quietly in the system tray.

<img width="512" height="760" alt="Night Ember settings" src="https://github.com/user-attachments/assets/b94e9c10-13b0-4064-8588-81a04fb1773a" />

## Features

- Adjustable warmth from 6500 K down to 1200 K
- Adjustable brightness from 100% (no dimming) down to 50%, using software dimming
- Manual, sunset-to-sunrise, and custom-hour schedules
- Offline sunrise and sunset estimates with no location permission or network access
- Smooth, configurable transitions
- Multi-monitor support
- Automatic recovery after display changes, unlock, or resume
- Gamma drift detection when another program resets a display
- Tray toggle, settings window, and optional sign-in startup
- Settings and tray-menu styling that follows the Windows light or dark app theme
- Single-instance behavior
- A cleanup watchdog that restores neutral gamma after an unexpected process exit
- Portable settings stored beside the executable

## Use

1. Turn off Windows Night Light and other gamma-adjustment tools so they do not compete with Night Ember.
2. Run `NightEmber.exe`.
3. Adjust the settings and select **Save**.
4. Use the crescent moon in the system tray to toggle the effect, reopen settings, or exit.

Warmth and brightness changes are previewed immediately and remain active while the settings window is open. Select
**Save** to keep them; select **Cancel** or close the window to restore the active saved state. Using the tray toggle
while the window is open ends the preview and applies a manual override.

Closing the settings window leaves Night Ember running in the tray. Choose **Exit**
from the tray menu to stop the application and restore neutral display gamma.

A manual tray toggle temporarily overrides an active schedule. The override expires when the schedule next changes
naturally.

## Keyboard access

The settings window starts with focus on Strength. Use **Tab** and **Shift+Tab** to move through settings and the
Save/Cancel buttons. Only the selected schedule option is a tab stop; use the arrow keys to select another schedule.
Disabled custom-time fields are skipped.

| Shortcut              | Action                                          |
|-----------------------|-------------------------------------------------|
| Alt+G / Alt+B         | Focus Strength / Brightness                     |
| Alt+N / Alt+U / Alt+H | Select manual / sunset / custom-hour scheduling |
| Alt+O / Alt+F         | Focus the custom turn-on / turn-off time        |
| Alt+T                 | Focus transition duration                       |
| Alt+A                 | Toggle start at sign-in                         |
| Alt+S / Alt+C         | Save / Cancel                                   |

In time fields, **Left/Right** selects the hour, minute, or AM/PM segment and **Up/Down** adjusts it. In the transition
field, **Up/Down** adjusts the duration by 50 milliseconds. Modified arrow keys retain normal text-editing behavior.
While typing a time, **Left/Right** moves the caret so you can correct the text; committing a valid edit restores
segment navigation. Inside these fields, **Enter** commits the edit and **Escape** restores it; use **Alt+S** or
**Alt+C** to save or cancel the whole window. Elsewhere, Enter and Escape activate the default Save and Cancel actions
when not consumed by a control.

Invalid times and transition durations stay visible when you press Enter or leave the field. A themed error outline and
inline message explain what needs correcting; Save keeps the window open and focuses the first invalid input. Correct
the text or press Escape in the field to restore its last accepted value. Transition durations must be whole numbers
from 0 to 5000 ms. Custom turn-on/off times must differ; unused custom-time fields do not block saving manual or sunset
schedules.

For the tray icon, press **Win+B**, then use the arrow keys to select Night Ember (open the hidden-icons area if
needed). **Enter** opens settings; **Shift+F10** or the context-menu key opens its menu. In the menu, use **Up/Down**
and **Enter**, or press **T** to toggle the tint, **S** for settings, and **X** to exit. **Escape** dismisses the menu
and returns focus to the notification area.

## Portable settings

Settings are saved as `NightEmber.config.json` beside `NightEmber.exe`:

| Setting       | Range                        | Default  | Purpose                                   |
|---------------|------------------------------|----------|-------------------------------------------|
| `Temperature` | 1200-6500                    | `3400`   | Color temperature while enabled           |
| `Brightness`  | 50-100                       | `100`    | Brightness percentage (100% = no dimming) |
| `Mode`        | `Manual`, `Sunset`, `Custom` | `Sunset` | Scheduling mode                           |
| `CustomOn`    | `HH:mm`                      | `21:00`  | Custom schedule start                     |
| `CustomOff`   | `HH:mm`                      | `07:00`  | Custom schedule end                       |
| `FadeMs`      | 0-5000                       | `300`    | Transition duration                       |

The executable must be in a folder the current user can write to. A folder under the user profile is recommended;
protected folders such as `Program Files` prevent the portable configuration from being saved.

Configuration values are validated when loaded. Invalid values use safe defaults, and malformed or inaccessible files
produce a visible warning. Saves use a temporary file and atomic replacement to reduce the chance of corruption.

## Sunset schedule

Sunrise and sunset are calculated locally. Night Ember uses a representative latitude for recognized Windows time zones
and derives longitude from the UTC offset. Unknown time-zone IDs use a clearly labeled equatorial estimate rather than
guessing a hemisphere. This avoids network access and location permissions, but the estimate can differ from local solar
time, especially when an equatorial fallback is used.

## Start at sign-in

Select **Start automatically when I sign in** and save. Night Ember creates a shortcut in the current user's Startup
folder with the `--hidden` option. Clearing the setting removes that shortcut. No elevation or registry change is used.

## Build

Requirements:

- Windows
- .NET 10 SDK

```powershell
dotnet build .\NightEmber.slnx
```

## Publish a self-contained portable executable

```powershell
dotnet publish .\src\NightEmber\NightEmber.csproj --configuration Release /p:PublishProfile=win-x64 /p:RestoreLockedMode=true
```

Output is written to:

```text
src\NightEmber\bin\publish\win-x64
```

The published `NightEmber.exe` contains the .NET runtime and targets 64-bit Windows. No separate runtime installation or
application installer is required.

## Display behavior and cleanup

Gamma ramps live in the display driver rather than the application process. Night Ember therefore starts a second,
minimal instance of its own executable as a watchdog. Normal shutdown signals the watchdog and restores neutral gamma
directly. If the main process is terminated unexpectedly, the watchdog detects that exit and restores every display.

Some display drivers reject strong gamma ramps. Night Ember clamps channel values to the minimum accepted by Windows and
retries once after rebuilding stale display handles. Hardware and driver behavior can still limit the visible strength
on some systems.

Windows applies gamma ramps globally through a legacy, driver-dependent API that can take up to 200 milliseconds on some
hardware. The display may briefly flicker or appear gray while a ramp is applied or restored. Windows can also reset the
ramp during display changes, and behavior is undefined with HDR or other color-calibration software. See Microsoft's
[`SetDeviceGammaRamp` documentation](https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp).

Force-stopping an entire process tree, as some IDE **Stop** commands do, can terminate both Night Ember and its watchdog
before either can restore neutral gamma. Exit through the tray before stopping a debug session. If a tint remains,
relaunch Night Ember and choose **Exit**.

Software dimming reduces the display signal, not the physical backlight. Lowering a monitor's actual backlight is
generally preferable when available.
