# JFD-28 Courier 0.44.0

Courier-only distribution for Nuclear Option 0.34.6. **Balanced is fixed.**
Bastion and the Performance/Detailed choices are not available in this package.
The original Courier turret options, cargo/weapon selectors and liveries remain.

## Install

Requires an installed copy of Nuclear Option, BepInEx, Blueprinter 2.1.1 and
HeavyHangar 0.2.0. Dependencies are not bundled. Close the game, then import the
release ZIP through your mod manager, replacing the older Courier/Bastion
package. Do not keep two copies of the old and new package active together.
For manual installation, run Install.ps1 with PowerShell7 and -GamePath pointing
to the game's directory. The installer backs up the previous package outside
the plugins tree and preserves configuration files and other mods.

Balanced is built in; there is no quality dropdown. An old saved Performance or
Detailed setting is ignored. F1 still contains the remaining mod settings when
BepInEx ConfigurationManager is installed separately. Native resistance and
loadout choices are not changed by this release restriction.

## Validation and limits

Private native single-player testing checked the sole Courier catalogue entry,
absence of a quality selector, an old Performance setting, paint changes,
selected weapons/turrets, support failure, ramp repair and physical gear debris
against a barrier. The aircraft retains 122 native parts and 305 structural contacts.
Impact frame-time spikes remain under investigation. This is a prerelease;
low-end PC performance and multiplayer are not certified by this validation.

## Download and source scope

Download the mod ZIP from the [v0.44.0 prerelease](https://github.com/jptecr2314/JFD-28-Courier/releases/tag/v0.44.0).
The release also includes a separate runtime source ZIP. This repository contains
that runtime source snapshot and build instructions, not the full Blender or
Unity authoring project. Game, Unity and third-party plugin binaries are not
included in the source snapshot.

See VALIDATION.json for the tested binary hashes. SHA256SUMS.txt is included in
the mod ZIP.

## Build the runtime source

Use Windows, PowerShell7, a C# compiler supporting the source's language version,
and the managed assemblies/BepInEx from your own installed game. Supply csc.exe
through -CompilerPath; compiler and game DLLs are not redistributed here.

```powershell
./Build.ps1 -GamePath 'C:/Games/Nuclear Option' -CompilerPath 'C:/SDK/Roslyn/csc.exe' -BundlePath 'C:/Downloads/JFD-28 Courier_0.44.0.nobp'
```

Extract the prebuilt .nobp from the tested release ZIP. The wrapper always builds
with COURIER_ONLY and COURIER_BALANCED_ONLY. Shared source helpers may retain
historical internal names; only the Courier hooks are enabled in this build.
Building the model bundle itself requires the separate authoring project; it
is not reconstructed by this runtime source snapshot.

GitHub file-size guidance: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github
