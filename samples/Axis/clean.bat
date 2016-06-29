@echo off

set SAMPLEDIR=.
cd %SAMPLEDIR%

if NOT "x%1" == "x-w" (
   if exist converter.jar del /F converter.jar
   if exist com\vmware\converter rmdir /q/s com\vmware\converter >nul 2>nul
   if exist com\vmware\converter rmdir /q/s com\vmware\converter >nul 2>nul
)

:CLEANEND
