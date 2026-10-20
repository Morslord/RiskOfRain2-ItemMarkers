# Building ItemMarkers 1.0.0

ItemMarkers targets .NET Standard 2.1 and references the local Risk of Rain 2 assemblies. The automated checks target .NET 10 and run without launching the game.

## Build requirements

- .NET SDK 10.
- A local Risk of Rain 2 installation with its managed assemblies.
- A BepInEx-enabled mod manager profile.
- `../build/icon.png` for the embedded mod icon. Preserve this repository-relative path.

Risk Of Options is optional at runtime and is not required to compile the mod. No framework/game DLLs are bundled or copied to the build output.

## Build and checks

Open `ItemMarkers.slnx` in Visual Studio, or run these commands **from `src/`**. Replace the example paths with your own game and profile directories.

```powershell
dotnet clean ItemMarkers.slnx
dotnet build ItemMarkers.slnx -c Release `
  "-p:GameDir=C:\Path\To\Risk of Rain 2" `
  "-p:ProfileDir=C:\Path\To\ModProfile"
dotnet run --project tests/CoreChecks/CoreChecks.csproj -c Release --no-build
```

The compiled plugin is written to `bin/Release/netstandard2.1/ItemMarkers.dll`. Release builds also copy it to `../build/ItemMarkers.dll`. Building does not install the plugin into a game profile or publish it.

The dependency-free executable checks classification, category/variant filters, nearest-marker selection, proximity boundaries, projection and asynchronous cache sharing/failure/cleanup. Failed assertions exit with an error. These checks do not execute Unity, validate native addressable loading, or replace in-game and multiplayer tests.

## Source layout

```text
src/
  ItemMarkers.csproj
  ItemMarkers.slnx
  Directory.Build.props
  ItemMarkersPlugin.cs
  MarkerSettings.cs
  Core/
  Game/
  Integrations/
  tests/CoreChecks/
  tools/
```

- `Core/`: rules and generic asset-cache behavior shared with the checks.
- `Game/`: pickup discovery, icon loading and local HUD rendering.
- `Integrations/`: optional Risk Of Options registration.
- `tests/CoreChecks/`: console check runner, linking the same core source files.
- `tools/`: optional local inspection/audit helpers, excluded from public upload and compilation.

Only the single `ItemMarkers.dll` is required at runtime. Keep `THIRD_PARTY_NOTICES.md`, which includes the original ItemShareFix license, with distributions. Generated `bin/`, `obj/` and `.vs/` directories are not required to rebuild the project.

Settings use `BepInEx/config/local.itemmarkers.cfg`. On first use, an existing legacy GUID-named configuration is copied only if the new file does not exist; the original file remains unchanged.
