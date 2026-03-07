@echo off
REM Setup DeepBim-MCP add-in for Revit 2025
REM Double-click to run, or run from Developer Command Prompt

powershell -ExecutionPolicy Bypass -File "%~dp0setup-revit-addin.ps1"
pause
