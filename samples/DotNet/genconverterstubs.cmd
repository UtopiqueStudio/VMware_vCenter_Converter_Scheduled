@setlocal
@echo off
@rem Generate WebService Stubs and precompiled stub serializers

@if not defined VSINSTALLDIR goto err_no_VCENVSETUP
if not exist %VSINSTALLDIR%\SDK\v2.0\bin\wsdl.exe goto err_no_VSTOOLS
if not exist %WINDIR%\Microsoft.NET\Framework\v2.0.50727\csc.exe goto err_no_VSTOOLS

@rem This script takes 6 parameters
@rem 1. directory of wsdl files. This is a mandatory parameter.
@rem 2. namespace for stubs
@rem     This defaults to ConverterApi
@rem 3. stub output directory
@rem     This defaults to .
@rem 4. output filename
@rem     This defaults to ConverterObjects.cs
@rem 5. dll directory
@rem     This defaults to .
@rem 6. Name of the DLLs to generate.
@rem     This defaults to ConverterSDKStub*

@rem this script assumes that the files are named converter.wsdl and converterService.wsdl
@if [%1]==[] goto err_no_WSDLFILE
@if not exist %1\converter.wsdl goto err_no_WSDLFILE
@if not exist %1\converterService.wsdl goto err_no_WSDLFILE
@set _WSDLDIR=%1

@if [%2]==[] (
   @set _NAMESPACE=ConverterApi
) else (
   @set _NAMESPACE=%2
)

@if [%3]==[] (
   @set _STUBDIR=.
) else (
   @set _STUBDIR=%3
)

@if [%4]==[] (
   @set _STUBFILENAME=ConverterObjects.cs
) else (
   @for /f "tokens=1" %%G in ('echo %~n4') do (
      @set _STUBFILENAME=%%G
      @call :set_filename %%G
   )
   @goto done_setfilename

:set_filename
   @set _STUBFILENAME=%1.cs
   @goto :eof

:done_setfilename
   @set dummy=
)

@if [%5]==[] (
   @set _DLLDIR=.
) else (
   @set _DLLDIR=%5
)

@if [%6]==[] (
   @set _DLLNAME=ConverterSDKStub
) else (
   @set _DLLNAME=%6
)

@set _LASTPASS=no

@if "%_STUBDIR%"=="." goto create_dll_dir
echo Checking and Creating %_STUBDIR%
@if not exist %_STUBDIR% (
  @rd /s/q %_STUBDIR%
  @if exist %_STUBDIR% (
    @rd /s/q %_STUBDIR%
  )
)
@md %_STUBDIR%

:create_dll_dir
@if "%_DLLDIR%"=="." goto gen_converter_stubs
echo Checking and Creating %_DLLDIR%
@if not exist %_DLLDIR% (
  @rd /s/q %_DLLDIR%
  @if exist %_DLLDIR% (
    @rd /s/q %_DLLDIR%
  )
)
@md %_DLLDIR%

:gen_converter_stubs

@echo generate the Converter C# stub file
wsdl.exe /l:CS /n:%_NAMESPACE% /out:%_STUBDIR%\%_STUBFILENAME% %_WSDLDIR%\converter.wsdl %_WSDLDIR%\converterService.wsdl %_WSDLDIR%\converter-types.xsd %_WSDLDIR%\converter-messagetypes.xsd %_WSDLDIR%\core-types.xsd %_WSDLDIR%\vim-types.xsd %_WSDLDIR%\query-messagetypes.xsd %_WSDLDIR%\query-types.xsd

:compile_DLL
@echo compile the stub dll
csc.exe /t:library /out:%_DLLDIR%\%_DLLNAME%.dll %_STUBDIR%\%_STUBFILENAME%
@if "%_LASTPASS%"=="yes" goto end_ok

@echo use sgen tool to pre-generate and compile Xml Serializers
sgen.exe /p /out:%_DLLDIR% %_DLLDIR%\%_DLLNAME%.dll

@echo Optimizing generated stubs...
@echo comment out all [System.Xml.Serialization.XmlIncludeAttribute] lines
OptimizeWsStubs.exe %_STUBDIR%\%_STUBFILENAME% %_DLLNAME%
@set _LASTPASS=yes
goto compile_DLL

:err_no_STUBDIR
@echo Error: Directory to build stubs in %_STUBDIR% does not exist
goto end_err

:err_no_DLLDIR
@echo Error: Directory to compile DLLs in %_DLLDIR% does not exist
goto end_err

:err_no_WSDLFILE
@echo Error: Directory for WSDL files converter.wsdl and converterService.wsdl not specified
@echo        Please specify WSDL files to generate stubs for
@echo Run As: genconverterstubs.cmd <WSDL Directory> [<Stub Namespace>] [<Stub file output directory>] [<Stub filename with .cs>] [<Dll output directory>] [<Stub Dll name(without .dll extension)>]
@echo E.g.
@echo         genconverterstubs.cmd .\
@echo         Or to customize namespace stubfilename and dll names etc...
@echo         genconverterstubs.cmd .\ ConverterApi .\stubdir MyConverterStubs.cs .\dlldir MyConverterStubs
@echo
goto end_err

:err_no_VCENVSETUP
@echo Error: No Visual Studio 2005 environment settings found
@echo        Please run this script inside a Visual Studio 2005 Command Prompt
@echo
goto end_err

:end_err
@echo Stub generation Failed!
@echo
@endlocal
exit /b 1

:end_ok
@echo Stub generation Done.
@echo
@endlocal
