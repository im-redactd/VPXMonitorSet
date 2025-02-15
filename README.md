# VPX Monitor Configuration Utility

**VPX Monitor Configuration Utility** is a command-line tool for automatically configuring Visual Pinball X (VPX) display settings based on your monitor’s resolution and name criteria. It is particularly useful for multi-monitor setups where the primary display may change between sessions.

## Overview

This utility detects connected monitors, selects the appropriate one based on resolution or name, and updates the VPX configuration (both registry and INI file). Additionally, it configures Future Pinball’s playfied and optionally the backbox monitor settings.

## Features

- **Automatic Display Detection:** Scans and identifies all connected monitors.
- **Configuration Updates:** Modifies VPX settings via the registry (`HKCU\SOFTWARE\Visual Pinball\VP10\Player`) and the INI file (`%APPDATA%\VPinballX\VPinballX.ini`).
- **Customizable Targeting:** Set target resolution (default is `3840` pixels) or match monitors by name.
- **Future Pinball Support:** Optionally configure the backbox monitor (registry: `HKCU\SOFTWARE\Future Pinball\GamePlayer`).
- **Debug and Logging:** Enable detailed output and file logging for troubleshooting.

## Command-Line Options

### General Options

- `-h`, `--help`  
  Display this help message and exit.

- `--debug`  
  Enable detailed debug output.

- `--log`  
  Enable logging to a file.

### Playfield Monitor Options

- `-r`, `--resolution <value>`  
  Set the target resolution (in pixels width) for the playfield monitor.  
  **Default:** `3840`

- `-pn`, `--playfield-name <name>`  
  Specify a substring to match the playfield monitor’s name.

### Future Pinball Options

- `-b`, `--backbox <value>`  
  Set the target resolution for the Future Pinball backbox monitor.

- `-bn`, `--backbox-name <name>`, `--bn <name>`  
  Specify a substring to match the backbox monitor’s name.

## Usage Examples

- **Default Execution:**  
  Run with default playfield resolution:
  ```sh
  VPXMonitorSet.exe
  ```

- **Custom Resolution:**  
  Target a playfield monitor with a different resolution:
  ```sh
  VPXMonitorSet.exe -r 1920
  ```

- **Name Matching & Future Pinball Setup:**  
  Match monitors by name and configure Future Pinball’s backbox:
  ```sh
  VPXMonitorSet.exe -pn "LG" -b 1080 -bn "INSIGNIA"
  ```

- **Debug and Logging Mode:**  
  Enable verbose output and file logging:
  ```sh
  VPXMonitorSet.exe --debug --log
  ```

## Installation

1. **Download the Latest Release:**  
   Obtain the executable from the releases page.

2. **Place the Executable:**  
   Move the executable to your desired folder.

## Automatic Startup

To have the utility run at Windows startup:

1. **Create a Batch File:**  
   Create a file (e.g., `vpx-monitor-config.bat`) with:
   ```batch
   @echo off
   cd /d "C:\Path\To\Your\Folder"
   start "" /B "VPXMonitorSet.exe"
   ```
2. **Add to Startup:**  
   Place a shortcut to this batch file in:
   ```
   %APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
   ```

*Note:* If Windows Defender interferes with executables placed directly in the Startup folder, use this batch file method.

## Configuration Files Modified

- **Visual Pinball X:**
  - **Registry:** `HKEY_CURRENT_USER\SOFTWARE\Visual Pinball\VP10\Player`
  - **INI File:** `%APPDATA%\VPinballX\VPinballX.ini`

- **Future Pinball (if configured):**
  - **Registry:** `HKEY_CURRENT_USER\SOFTWARE\Future Pinball\GamePlayer`

## Troubleshooting

- **Monitor Detection Issues:**  
  Run with the `--debug` flag to review detailed output and verify that your target resolution or name criteria match the actual monitor settings.

- **Registry/INI Updates Not Applied:**  
  Ensure the utility has the necessary permissions and that you’re using the correct registry paths.

- **Windows Defender Blockage:**  
  If the executable is blocked, relocate it and run via the batch file method described above.

## Credits

This utility was inspired by a solution that leverages DirectX output to identify the correct monitor. For more details, please refer to the original article: [Who Stuck the Pinball on My Backbox?](https://greatjava.org/2022/04/who-stuck-the-pinball-on-my-backbox/).
