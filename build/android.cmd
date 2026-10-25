@echo off
rem Builds kofrenot for Android (src\kofrenot.Android, not in kofrenot.sln):
rem artifacts\publish\kofrenot.Android\release\eu.dlabs.kofrenot-Signed.apk.
rem Needs the .NET Android workload (dotnet workload install android), the Android SDK and a JDK.
rem Signed with the release key when KOFRENOT_KEYSTORE names its keystore (alias "kofrenot", its
rem password in KOFRENOT_KEYSTORE_PASS: never written here); else with Android's debug key, to try it.
setlocal
cd /d "%~dp0.."

set SIGNING=
if defined KOFRENOT_KEYSTORE set SIGNING=-p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=%KOFRENOT_KEYSTORE%" -p:AndroidSigningKeyAlias=kofrenot -p:AndroidSigningStorePass=env:KOFRENOT_KEYSTORE_PASS -p:AndroidSigningKeyPass=env:KOFRENOT_KEYSTORE_PASS
if not defined KOFRENOT_KEYSTORE echo No KOFRENOT_KEYSTORE: signed with the debug key, not for release.

dotnet publish src\kofrenot.Android\kofrenot.Android.csproj -c Release %SIGNING% || exit /b 1
echo APK: artifacts\publish\kofrenot.Android\release\eu.dlabs.kofrenot-Signed.apk
