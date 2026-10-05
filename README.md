# 🌙 Night Ember

Night Ember is a portable night-light application for Windows. It applies a warm color temperature directly through each
display's gamma ramp and runs in the system tray.

<img width="512" height="760" alt="Night Ember settings" src="https://github.com/user-attachments/assets/b94e9c10-13b0-4064-8588-81a04fb1773a" />

## Features

* Adjustable warmth from 6500 K down to 1900 K
* Adjustable brightness from 100% (no dimming) down to 50%, using software dimming
* Manual, sunset-to-sunrise, and custom-hour schedules
* Offline sunrise and sunset estimates with no location permission or network access
* Gradual transitions with a configurable duration
* Multi-monitor support
* Automatic recovery after display changes, unlock, or resume
* Schedule updates when the Windows time or time zone changes
* Gamma drift detection when another program resets a display
* Tray toggle, settings window, and optional sign-in startup
* Settings and tray-menu styling that follows the Windows light or dark app theme
* One instance per Windows session; launching it again opens the running copy's settings
* A cleanup watchdog that restores neutral gamma after an unexpected process exit
* Portable settings stored beside the executable

## Use

1. Turn off Windows Night Light and other gamma-adjustment tools so they do not compete with Night Ember.
2. Run `NightEmber.exe`. Settings open automatically on the first run.
3. Adjust the settings and select **Save**.
4. Click the crescent moon in the system tray to reopen settings, or right-click it to toggle the tint or exit.

Warmth and brightness changes preview immediately while the settings window is open. Select **Save** to keep them;
select **Cancel** or close the window to restore the saved state.

Closing the settings window leaves Night Ember running in the tray. **Exit** in the tray menu stops it and restores
neutral gamma.

The tray toggle overrides the schedule until the schedule next changes on its own or you save settings. Using it while
the settings window is open also ends the preview.

## Keyboard access

The settings window starts with focus on Strength. Use Tab and Shift+Tab to move through settings and the Save/Cancel
buttons. Only the selected schedule option is a tab stop; use the arrow keys to select another schedule.
Disabled custom-time fields are skipped.

| Shortcut              | Action                                          |
|-----------------------|-------------------------------------------------|
| Alt+G / Alt+B         | Focus Strength / Brightness                     |
| Alt+N / Alt+U / Alt+H | Select manual / sunset / custom-hour scheduling |
| Alt+O / Alt+F         | Focus the custom turn-on / turn-off time        |
| Alt+T                 | Focus transition duration                       |
| Alt+A                 | Toggle start at sign-in                         |
| Alt+S / Alt+C         | Save / Cancel                                   |

In time fields, Left/Right selects the hour, minute, or AM/PM segment and Up/Down adjusts it (minutes move in steps of
5). In the transition field, Up/Down adjusts the duration by 50 milliseconds. Modified arrow keys retain normal
text-editing behavior. While typing a time, Left/Right moves the caret so you can correct the text; committing a valid
edit restores segment navigation. Inside these fields, Enter commits the edit and Escape restores it; use Alt+S or Alt+C
to save or cancel the whole window. Elsewhere, Enter and Escape activate the default Save and Cancel actions when not
consumed by a control.

Invalid times and transition durations stay visible when you press Enter or leave the field. A themed error outline and
inline message explain what needs correcting; Save keeps the window open and focuses the first invalid input. Correct
the text or press Escape in the field to restore its last accepted value. Transition durations must be whole numbers
from 0 to 5000 ms. Custom turn-on/off times must differ; unused custom-time fields do not block saving manual or sunset
schedules.

For the tray icon, press Win+B, then use the arrow keys to select Night Ember (open the hidden-icons area if needed).
Enter opens settings; Shift+F10 or the context-menu key opens its menu. In the menu, use Up/Down and Enter, or press T
to toggle the tint, S for settings, and X to exit. Escape dismisses the menu and returns focus to the notification area.

## Portable settings

Settings are saved as `NightEmber.config.json` beside `NightEmber.exe`:

| Setting       | Range                        | Default  | Purpose                                   |
|---------------|------------------------------|----------|-------------------------------------------|
| `Temperature` | 1900-6500                    | `3400`   | Color temperature while enabled           |
| `Brightness`  | 50-100                       | `100`    | Brightness percentage (100% = no dimming) |
| `Mode`        | `Manual`, `Sunset`, `Custom` | `Sunset` | Scheduling mode                           |
| `CustomOn`    | `HH:mm`                      | `21:00`  | Custom schedule start                     |
| `CustomOff`   | `HH:mm`                      | `07:00`  | Custom schedule end                       |
| `FadeMs`      | 0-5000                       | `300`    | Transition duration                       |

The executable must be in a folder the current user can write to. A folder under the user profile is recommended;
protected folders such as `Program Files` prevent the portable configuration from being saved.

Configuration values are validated when loaded. Invalid values use safe defaults, and malformed or inaccessible files
produce a visible warning. Saves use a temporary file and atomic replacement to reduce the chance of corruption.
Temperatures from 1200 K to 1899 K saved by earlier versions are raised to 1900 K, the warmest setting drivers can
display distinctly.

## Sunset schedule

Sunrise and sunset are calculated locally. Night Ember uses a representative latitude for recognized Windows time zones
and derives longitude from the UTC offset. Unrecognized time zones fall back to latitude zero, shown as an "equatorial
estimate" in settings, rather than guessing a hemisphere. This avoids network access and location permissions, but the
estimated times can differ from actual local sunrise and sunset, especially with the equatorial fallback.

## Start at sign-in

Select **Start automatically when I sign in** and save. Night Ember creates a shortcut in the current user's Startup
folder with the `--hidden` option. Clearing the setting removes that shortcut. No elevation or registry change is used.

Only one Night Ember startup shortcut exists per user. The setting shows as enabled only when that shortcut launches the
copy you are configuring; clearing it leaves a shortcut that launches another existing copy untouched. A shortcut that
points to a moved or deleted copy is replaced or removed.

## Verify a download

Each release includes `NightEmber.exe.sha256` and a GitHub build-provenance attestation. To confirm the file is intact,
compare its hash with the value in `NightEmber.exe.sha256`:

```powershell
(Get-FileHash .\NightEmber.exe -Algorithm SHA256).Hash
```

To confirm the file was built by this repository's release workflow, use the GitHub CLI:

```powershell
gh attestation verify .\NightEmber.exe --repo austins/NightEmber
```

The executable is not code-signed, so Windows SmartScreen may still warn about an unknown publisher.

## Build

Requires Windows and the .NET 10 SDK.

```powershell
dotnet build .\NightEmber.slnx
```

## Publish a self-contained portable executable

```powershell
dotnet publish .\src\NightEmber\NightEmber.csproj --configuration Release /p:PublishProfile=win-x64 /p:RestoreLockedMode=true
```

The output, `src\NightEmber\bin\publish\win-x64\NightEmber.exe`, bundles the .NET runtime and targets 64-bit Windows,
so it needs no installer or separate runtime.

## Display behavior and cleanup

Gamma ramps outlive the process that sets them, so Night Ember starts a watchdog. If Night Ember ends unexpectedly, the
watchdog restores every display.

If the watchdog is also terminated, for example from Task Manager, a tint can remain. Relaunch Night Ember and choose
**Exit** to restore neutral gamma.

When Windows begins signing out or shutting down, Night Ember restores neutral gamma and exits. If the sign-out or
shutdown is then cancelled, start Night Ember again to restore the tint.

If Night Ember crashes, the unhandled error is appended to `NightEmber.log` beside the executable. The log starts over
once it reaches 1 MB.

Some display drivers reject strong gamma ramps. Night Ember keeps every channel at or above half strength, the minimum
accepted by Windows. Hardware and driver behavior can still limit the visible strength on some systems.

Because of that floor, brightness dims only the range above half strength, and warm colors become less saturated as
brightness decreases. At 50% brightness every channel sits at the floor, so the display is dimmed evenly without a warm
tint. Software dimming reduces the display signal, not the backlight; use the monitor's own brightness control when you
can.

Windows applies gamma ramps globally through a legacy, driver-dependent API that can take up to 200 milliseconds on some
hardware. The display may briefly flicker or appear gray while a ramp is applied or restored. Windows can also reset the
ramp during display changes, and behavior is undefined in HDR mode or alongside color-calibration software. See
Microsoft's
[`SetDeviceGammaRamp` documentation](https://learn.microsoft.com/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp).
