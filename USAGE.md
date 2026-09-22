# SF6 USB Guard 0.2

## Start

1. Extract the ZIP. Keep both EXE files together.
2. Close SF6. If an older version is protecting devices, let it restore them and close its window first.
3. Open SF6-USB-Guard.exe. The app reads the connected USB inventory without disabling devices.
4. Choose English (US) or 繁體中文 in the top-right menu. Your choice is saved.
5. Click **Check & launch SF6**, then allow administrator access.

The app checks the current USB HID interfaces, pauses eligible affected interfaces if needed, and launches SF6 through Steam. If no eligible affected interfaces are found, it leaves USB devices unchanged and still launches SF6. A failed, cancelled or timed-out check will not launch the game.

**Check again** only checks devices. It does not disable interfaces or launch the game. **Details** shows the inventory and results. Device names supplied by Windows may remain in your Windows language.

## What it changes

An interface must produce two slow or timed-out product-name queries before it can be selected. Slow means at least 1.5 seconds; the product-name query times out after 2.5 seconds. Each query runs in a separate process, with up to four running at once.

Only these auxiliary HID interfaces can be paused:

- Consumer controls: Usage 000C / 0001. Media and volume keys may stop working temporarily.
- System controls: Usage 0001 / 0080. Power and sleep keys may stop working temporarily.
- Two known vendor interfaces on 36B0:3002 receivers: MI_01 with FF60 / 0061, and MI_02 & Col05 with FF00 / 0001. Settings and auxiliary features may be unavailable temporarily.

The app also checks the setup class, USB ancestry and collection layout. Primary keyboards, mice, game controllers, USB parents, hubs and unknown-purpose interfaces are not automatically disabled. Already-disabled interfaces are left alone. An access error or an unconfirmed query is not enough to select a device.

A check supports up to 128 HID interfaces and has a 90-second limit. If the device inventory changes during a check, the plan is discarded. Recheck after reconnecting devices or changing ports; the app does not disable newly connected interfaces during a game.

## Restore

- After SF6 exits, the app waits about 8 seconds and restores this session's changes.
- If SF6 does not start within 3 minutes after protection is ready, devices are restored.
- Closing the main window does not stop the background guardian from restoring devices after the game exits.
- To recover after an interruption, reopen the app from the same folder, close SF6 and click **Restore**.
- If a device is missing or moved to another port, reconnect it to its original port and retry.
- Keep the Data folder while any restoration is pending. It contains the recovery record and its backup.
- Disabling is not marked persistent across reboot. A normal Windows restart re-enables these temporary changes; the app can then clear the completed recovery record.

Version 0.2 can read the original version's limited restore records. Complete restoration with the old version before upgrading. Data folders in different locations are not merged automatically.

## Limits

The app does not block USB notifications to the game. It tries to keep slow auxiliary HID queries out of later device scans. It cannot prevent every USB-related freeze, and it cannot keep a physically disconnected controller sending input. Intermittent problems that do not appear during the check may go undetected.

Simulated tests, read-only USB enumeration and both language previews have been checked. Actual device disabling and in-game freeze prevention have not yet been verified for this version. Test in offline training first.

## Files

- SF6-USB-Guard.exe: main window and administrator guardian.
- HidProbe.exe: product-name query helper. It opens HID devices with no read/write access and sends no input, output or feature reports.
- Data/: local inventory, language preference, logs and recovery records. Nothing is uploaded.
- src/ and build.ps1: source, tests and build script. Building requires .NET Framework, Visual Studio C++ Build Tools and the Windows SDK.
- SHA256.txt: hashes of both executables.

No game injection, DLL replacement, driver installation or startup task. The executables are unsigned.

**Support future updates** opens https://buymeacoffee.com/tananis only when clicked. All features remain available without a donation.
