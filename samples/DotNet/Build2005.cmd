@setlocal
@echo off
@rem generates and compiles the .NET proxy classes and precompiled XmlSerializers
@rem and builds all the C# samples in debug mode.

@rem If you have installed VCS 2005 Express in a non-default location, then
@rem you should have set the VSINSTALLDIR as specified in the GeneratingStubs.txt
@rem set VSINSTALLDIR = "%ProgramFiles%\Microsoft Visual Studio 8" - the default
@if not defined VSINSTALLDIR (
   @set VSINSTALLDIR="%ProgramFiles(x86)%\Microsoft Visual Studio 8"
) else (
   @rem need the quotes to hide the spaces!
   @set VSINSTALLDIR=%VSINSTALLDIR%
  )
@echo VSINSTALLDIR=%VSINSTALLDIR%
@set DEVENV=MSBuild.exe

@rem Check If you have Visual C# 2005 Express or Visual Studio 2005 installed in the
@rem default "%ProgramFiles%\Microsoft Visual Studio 8" directory
@rem The .net framework 2.0 does not contain the wsdl.exe and sgen.exe.
@rem This is the reason why you need to have either of VS 2005 or VC# 2005 Express
@rem if neither of these 2 are found, exit with an error
if exist %VSINSTALLDIR%\Common7\IDE\VCSExpress.exe (
   @echo Visual Studio C# 2005 Express is installed
) else (
   if exist %VSINSTALLDIR%\Common7\IDE\devenv.exe (
      @echo Visual Studio 2005 is installed
   ) else (
      goto err_NO_DEVENV
   )
)

@echo Setting Path
@rem wsdl.exe and sgen.exe are contained in this folder
@set PATH=%PATH%;%VSINSTALLDIR%\SDK\v2.0\bin

@rem Assumes that .NET 2.0 is installed in the default location
@set PATH=%PATH%;%WINDIR%\Microsoft.NET\Framework\v2.0.50727

@rem for Visual Studio 2005, and Visual C# 2005 Express,
@rem INCLUDE, LIB and LIBPATH are not required
@rem since we're only building C# samples
@set INCLUDE=
@set LIB=
@set LIBPATH=

rem change to batch file directory and drive
call :get_cur_drive %CD%
%~d0
pushd %~p0

@rem generate optimized stubs and precompiled serializer dll for the Converter wsdl files
@rem and a DLL containing the optimized stubs named ConverterSDKStub, and
@rem a DLL containing precompiled XmlSerializers named ConverterSDKStub.XmlSerializers.dll
@call genconverterstubs.cmd ..\..\wsdl\converter ConverterApi stage ConverterObjects.cs . ConverterSDKStub
if ERRORLEVEL 1 goto err

@echo Building Samples in Debug mode
@if EXIST build.log del -f build.log
%DEVENV% cs\ConverterSamples.sln  /t:Rebuild /p:Configuration=Debug 1>build.log 2>&1


@echo Done Building optimized Stubs and all Samples
goto end

:err_no_DEVENV
@echo Error: No .NET Development environment or tools found installed
@echo        Visual Studio 2005 or Visual C# 2005 Express must be installed
@echo        -
@echo        If you have Visual Studio 2005 installed, please run this from
@echo        a Visual Studio 2005 Command Prompt.
@echo        -
@echo        If you have Visual C# 2005 Express and it is not installed in the
@echo        default location "%ProgramFiles%\Microsoft Visual Studio 8",
@echo        then please set the environment variable VSINSTALLDIR to the
@echo        directory that contains the 2 directories "Common7" and "SDK"
@echo        e.g. if Visual Studio is installed in the directory "All My Apps"
@echo        set VSINSTALLDIR="C:\All My Apps\Microsoft Visual Studio 8"
@echo        -
@echo        Please use quotes around directory names with spaces in them.

:err
exit /b 1

:end
popd
%CDRIVE%
set CDRIVE=
goto :EOF

:get_cur_drive
set CDRIVE=%~d1
@endlocal
