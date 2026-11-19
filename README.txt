TinyScholar Switcher v1.03 — compact toolbar spacing

Extract the ZIP and run the EXE. EXE and settings.json are at the ZIP root.
Keep your existing settings.json to retain saved paths and theme.

The sidebar keeps a minimum usable width. Pack cards use one to four columns
according to available space, reserving room for their icons. The header can
wrap, and selection buttons move below their heading in narrow windows.
Text sizes and font families are unchanged. Dark-mode dialogs now dim the
background subtly; theme switching remains available while dialogs are open.

Requires .NET Framework 4.8 or compatible 4.8.1; no bundled runtime.
Windows visual rendering needs verification; this build is cross-compiled.
Build source: BUILD_WINDOWS.bat. Tests: dotnet run --project Tests/EngineTests.csproj

Narrow-window toolbar saves 24 logical pixels without reducing button or text sizes.
4K geometry checked at 100%, 150%, and 200% scaling; actual Windows rendering
at 4K is not verified. Test long dialogs at small window sizes on Windows.
