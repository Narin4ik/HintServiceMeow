# Build references

References refreshed on 2026-11-17:

- `Exiled.API.dll` and `Exiled.Events.dll`: official [EXILED v9.15.3 release](https://github.com/ExMod-Team/EXILED/releases/tag/v9.15.3), `Exiled.tar.gz`.
- Game, Mirror, CommandSystem and Unity references: [EXILED SL-References Master.zip](https://Exmod-team.github.io/SL-References/Master.zip), the reference source used by EXILED's official build workflow. This URL is mutable; the checked-in DLLs pin this build's inputs.
- LabAPI: NuGet `Northwood.LabAPI` 1.1.7.

These DLLs are compile-time references, not additional plugins to install on the server.
Only install the resulting HSM DLL for your loader.
