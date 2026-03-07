@echo off
cd /d %~dp0
valkey-server.exe valkey.conf
pause
