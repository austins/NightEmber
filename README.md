# Night Ember

Night Ember is a lightweight, portable night-light application for Windows. It applies a warm color temperature directly
through each display's gamma ramp and runs quietly in the system tray.

## Features

- Adjustable warmth from 6500 K down to 1200 K
- Optional software dimming from 100% to 50%
- Manual, sunset-to-sunrise, and custom-hour schedules
- Offline sunrise and sunset estimates with no location permission or network access
- Smooth, configurable transitions
- Multi-monitor support
- Automatic recovery after display changes, unlock, or resume
- Gamma drift detection when another program resets a display
- Tray toggle, settings window, and optional sign-in startup
- Single-instance behavior
- A cleanup watchdog that restores neutral gamma after an unexpected process exit
- Portable settings stored beside the executable

## Use

1. Turn off Windows Night Light and other gamma-adjustment tools so they do not compete with Night Ember.
2. Run `NightEmber.exe`.
3. Adjust the settings and select **Save**.
4. Use the crescent moon in the system tray to toggle the effect, reopen settings, or exit.

Warmth and software-dimming changes are previewed immediately and remain active while the settings window is open.
Select **Save** to keep them; select **Cancel** or close the window to restore the active saved state.

Closing the settings window leaves Night Ember running in the tray. Choose **Exit**
from the tray menu to stop the application and restore neutral display gamma.

A manual tray toggle temporarily overrides an active schedule. The override expires when the schedule next changes
naturally.

## Portable settings

Settings are saved as `NightEmber.config.json` beside `NightEmber.exe`:

| Setting       | Range                        | Default  | Purpose                         |
|---------------|------------------------------|----------|---------------------------------|
| `Temperature` | 1200-6500                    | `3400`   | Color temperature while enabled |
| `Brightness`  | 50-100                       | `100`    | Software dimming percentage     |
| `Mode`        | `Manual`, `Sunset`, `Custom` | `Sunset` | Scheduling mode                 |
| `CustomOn`    | `HH:mm`                      | `21:00`  | Custom schedule start           |
| `CustomOff`   | `HH:mm`                      | `07:00`  | Custom schedule end             |
| `FadeMs`      | 0-5000                       | `300`    | Transition duration             |

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
dotnet publish .\src\NightEmber\NightEmber.csproj --configuration Release /p:PublishProfile=win-x64
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
