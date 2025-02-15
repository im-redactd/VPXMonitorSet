@echo off
cd /d "C:\vPinball\Tools"
start "" /B "VPXMonitorSet.exe" --debug > "%USERPROFILE%\vpx-monitor.log" 2>&1