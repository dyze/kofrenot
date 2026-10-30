@echo off
rem Builds kofrenot for Android (src\kofrenot.Android, not in kofrenot.sln):
rem artifacts\android\kofrenot-android-<version>.apk, or kofrenot-android-<version>-debug.apk.
rem   android.cmd            arm64 only: the phones, and the releases.
rem   android.cmd emulator   arm64 and x64, to try it in an emulator on a PC too: ...-emulator.apk.
rem Needs the .NET Android workload (dotnet workload install android), the Android SDK and a JDK.
rem Signed with the release key when KOFRENOT_KEYSTORE names its keystore (alias "kofrenot", its
rem password in KOFRENOT_KEYSTORE_PASS: never written here); else with Android's debug key, to try it.
setlocal
cd /d "%~dp0.."

set "PROJECT=src\kofrenot.Android\kofrenot.Android.csproj"

rem The processors (ABIs) in the APK are set in the project: arm64, with x64 too for an emulator.
set "EMULATOR=false"
set "KIND="
if /i "%~1"=="emulator" (
  set "EMULATOR=true"
  set "KIND=-emulator"
) else if not "%~1"=="" (
  echo Usage: build\android.cmd [emulator]
  exit /b 1
)

rem The version comes from Directory.Build.props, the single source of truth.
set "VERSION="
for /f "usebackq delims=" %%v in (`dotnet msbuild "%PROJECT%" -nologo -getProperty:Version`) do set "VERSION=%%v"
if not defined VERSION (
  echo Could not read the version from Directory.Build.props.
  exit /b 1
)

set SIGNING=
set "APK=kofrenot-android-%VERSION%%KIND%.apk"
if defined KOFRENOT_KEYSTORE set SIGNING=-p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=%KOFRENOT_KEYSTORE%" -p:AndroidSigningKeyAlias=kofrenot -p:AndroidSigningStorePass=env:KOFRENOT_KEYSTORE_PASS -p:AndroidSigningKeyPass=env:KOFRENOT_KEYSTORE_PASS
if not defined KOFRENOT_KEYSTORE (
  echo No KOFRENOT_KEYSTORE: signed with the debug key, not for release.
  set "APK=kofrenot-android-%VERSION%%KIND%-debug.apk"
)

rem From clean: what a failed build leaves can give an APK without its marshal methods (no native
rem onCreate: the app crashes at start, the build still succeeds).
for %%d in (obj bin publish) do if exist "artifacts\%%d\kofrenot.Android" rmdir /s /q "artifacts\%%d\kofrenot.Android"

dotnet publish "%PROJECT%" -c Release -p:KofrenotEmulator=%EMULATOR% %SIGNING% || exit /b 1

rem dotnet publish names the APK after the package id: copy it under the name the releases use.
if not exist artifacts\android mkdir artifacts\android
copy /y artifacts\publish\kofrenot.Android\release\eu.dlabs.kofrenot-Signed.apk "artifacts\android\%APK%" >nul || exit /b 1
echo APK: artifacts\android\%APK%
