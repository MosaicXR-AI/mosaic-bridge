# Building the Windows installer

Inno Setup only runs on Windows, so this step happens on the Windows machine. The
binary itself is cross-built anywhere.

1. On any machine, produce the executable:
   ```
   cd packages/connector
   npm install && npm run build
   npx pkg dist/index.js --targets node20-win-x64 --out-path build
   ```
   Copy `build/index-win-x64.exe` to the Windows machine, in the same folder layout.

2. On Windows, once:
   ```
   winget install JRSoftware.InnoSetup --accept-source-agreements --accept-package-agreements
   ```

3. Compile:
   ```
   iscc packaging\mosaic-connector.iss
   ```
   Result: `build\Mosaic-Connector-Setup.exe`.

The installer places one program in Program Files, optionally adds it to PATH and the
desktop, and offers to run `setup` at the end.

**Signing.** Unsigned, Windows SmartScreen shows a warning the user must click through.
An Authenticode certificate removes it:
```
signtool sign /fd SHA256 /a /tr http://timestamp.digicert.com /td SHA256 build\Mosaic-Connector-Setup.exe
```
For an evaluation the warning is acceptable; for a paid deployment it is worth buying
a certificate (about 200-400 USD a year), since a warning on first launch reads as
unprofessional to an enterprise IT team.

**MSI instead of EXE.** Some enterprises deploy only MSI. If Unity's IT asks, the same
binary can be wrapped with WiX Toolset; the layout is identical (one file in Program
Files plus a PATH entry), so it is a packaging change, not a product change.
