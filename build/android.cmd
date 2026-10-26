@echo off
rem Builds kofrenot for Android (src\kofrenot.Android, not in kofrenot.sln):
rem artifacts\android\kofrenot-android-<version>.apk, or kofrenot-android-<version>-debug.apk.
rem Needs the .NET Android workload (dotnet workload install android), the Android SDK and a JDK.
rem Signed with the release key when KOFRENOT_KEYSTORE names its keystore (alias "kofrenot", its
rem password in KOFRENOT_KEYSTORE_PASS: never written here); else with Android's debug key, to try it.
setlocal
cd /d "%~dp0.."

set "PROJECT=src\kofrenot.Android\kofrenot.Android.csproj"

rem The version comes from Directory.Build.props, the single source of truth.
set "VERSION="
for /f "usebackq delims=" %%v in (`dotnet msbuild "%PROJECT%" -nologo -getProperty:Version`) do set "VERSION=%%v"
if not defined VERSION (
  echo Could not read the version from Directory.Build.props.
  exit /b 1
)

set SIGNING=
set "APK=kofrenot-android-%VERSION%.apk"
if defined KOFRENOT_KEYSTORE set SIGNING=-p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=%KOFRENOT_KEYSTORE%" -p:AndroidSigningKeyAlias=kofrenot -p:AndroidSigningStorePass=env:KOFRENOT_KEYSTORE_PASS -p:AndroidSigningKeyPass=env:KOFRENOT_KEYSTORE_PASS
if not defined KOFRENOT_KEYSTORE (
  echo No KOFRENOT_KEYSTORE: signed with the debug key, not for release.
  set "APK=kofrenot-android-%VERSION%-debug.apk"
)

dotnet publish "%PROJECT%" -c Release %SIGNING% || exit /b 1

rem dotnet publish names the APK after the package id: copy it under the name the releases use.
if not exist artifacts\android mkdir artifacts\android
copy /y artifacts\publish\kofrenot.Android\release\eu.dlabs.kofrenot-Signed.apk "artifacts\android\%APK%" >nul || exit /b 1
echo APK: artifacts\android\%APK%
