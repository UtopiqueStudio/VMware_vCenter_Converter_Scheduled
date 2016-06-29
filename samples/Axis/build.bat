@REM you need to set 2 env variables : AXISHOME and JAVAHOME,
@REM or modify the 2 values here
@echo off

setlocal
set SAMPLEDIR=.
set SAMPLEJARDIR=.

if NOT DEFINED AXISHOME (
   @echo AXISHOME not defined. Must be defined to build web service apps.
   goto END_ERR
)

if NOT DEFINED JAVAHOME (
   @echo JAVAHOME not defined. Must be defined to build java apps.
   goto END_ERR
)

if NOT "x%1" == "x-w" (
   set WSDLFILECONVERTER=..\..\wsdl\converter\converterService.wsdl
)

:SETENV
set PATH=%JAVAHOME%\bin;%AXISHOME%\bin;%PATH%
@REM cleanup mail jar files. dont need all of them.

set LOCALCLASSPATH=%CD%;%AXISHOME%\lib\;%AXISHOME%\lib\activation.jar;%AXISHOME%\lib\axis.jar;%AXISHOME%\lib\axis-ant.jar;%AXISHOME%\lib\commons-codec-1.3.jar;%AXISHOME%\lib\commons-discovery-0.2.jar;%AXISHOME%\lib\commons-httpclient-3.0.1.jar;%AXISHOME%\lib\commons-httpclient-3.0-rc2.jar;%AXISHOME%\lib\commons-logging-1.0.4.jar;%AXISHOME%\lib\commons-net-1.0.0-dev.jar;%AXISHOME%\lib\jaxrpc.jar;%AXISHOME%\lib\junit-3.8.1.jar;%AXISHOME%\lib\log4j-1.2.8.jar;%AXISHOME%\lib\mailapi.jar;%AXISHOME%\lib\saaj.jar;%AXISHOME%\lib\wsdl4j-1.5.1.jar;

for %%i in ("lib\*.jar") do call lcp.bat %CD%\%%i

set LOCALCLASSPATH=%LOCALCLASSPATH%%CLASSPATH%

:DOBUILD
call clean.bat %1

cd %SAMPLEDIR%

if NOT "x%1" == "x-w" (
IF EXIST com\vmware\converter (
      rmdir/s/q com\vmware\converter
   )
   mkdir com\vmware\converter

   @echo Generating converter stubs from wsdl
   java -Xms1024M -Xmx1024M -Xss1024k -classpath "%LOCALCLASSPATH%" org.apache.axis.wsdl.WSDL2Java -w -O-1 -p com.vmware.converter -o. %WSDLFILECONVERTER%
   if ERRORLEVEL 1 goto END_ERR

   @echo Compiling stubs.
   javac -J-Xms1024M -J-Xmx1024M -classpath "%LOCALCLASSPATH%" com\vmware\converter\*.java
   if ERRORLEVEL 1 goto END_ERR

   jar cf %SAMPLEJARDIR%\converter.jar com\vmware\converter\*.class
   if ERRORLEVEL 1 goto END_ERR

   @echo Done compiling stubs.
)

@echo Compiling samples

javac -classpath "%LOCALCLASSPATH%" SubmitWinP2VJob\*.java

jar cf %SAMPLEJARDIR%\SubmitWinP2VJob.jar SubmitWinP2VJob\*.class

goto END

:END_ERR
@echo Failed.
exit /b 1

:END
@echo Done.
@endlocal
