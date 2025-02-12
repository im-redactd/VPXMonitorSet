# VPX Monitor Configuration Utility

A simple command-line utility for automatically configuring Visual Pinball X's display settings based on monitor resolution.

## Purpose

This tool helps Visual Pinball X users automatically set their preferred display for the game by detecting monitors with specific resolutions. It's particularly useful for multi-monitor setups where you want VPX to consistently launch on a specific display. This exists because some environments change the order of the primary monitor on restart.

## Features

- Automatically detects all connected displays

- Sets the target display in both VPinball registry and VPinballX.ini

- Configurable target resolution (defaults to `3840` pixels width)

- Debug mode for troubleshooting

- Can be run at startup or manually

## Usage

### Basic Use
Simply run the executable:
```
VPXMonitorSet.exe
```

### Custom Resolution
To target a different resolution:
```
VPXMonitorSet.exe -r 1920
```

### Debug Mode
To see detailed information about detected displays:
```
VPXMonitorSet.exe --debug
```

## Installation

Download the latest release

Place the executable in any folder

## Windows Defender?

I've run into issues placing this utility directly in the startup folder. Placing it in another location and calling it from a bat file seems to be a working process.

### Automatic Startup

To run at Windows startup:

Create a batch file (e.g., vpx-monitor-config.bat) with this content:
```
@echo off
cd /d "C:\Path\To\Your\Folder"
start "" /B "VPXMonitorSet.exe"
```
Place a shortcut to this batch file in your startup folder (`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`)
## Configuration Files Modified

The utility modifies two locations:

- Registry: `HKEY_CURRENT_USER\SOFTWARE\Visual Pinball\VP10\Player`

- INI File: `%APPDATA%\VPinballX\VPinballX.ini`

## Troubleshooting

If the utility isn't finding your displays:

- Run with --debug flag to see all detected displays.

- Check if your target resolution matches the actual display resolution

- If Windows Defender blocks the application:
   - Place the executable in a regular folder (not Startup)
   - Use the batch file method for startup configuration

## Credit
I ran across this article and solution which dumps the directx output and finds the correct monitor. I just took this concept and put it into a c# exe.
`https://greatjava.org/2022/04/who-stuck-the-pinball-on-my-backbox/`
  
