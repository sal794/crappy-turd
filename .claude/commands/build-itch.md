# Build WebGL for itch.io

Zips the latest WebGL build with a version timestamp, ready for itch.io upload.

Note: WebGL builds take several minutes and cannot be triggered programmatically due to tool timeouts. Instruct the user to build first via **Unity → File → Build Settings → Build** targeting `Builds/WebGL`, then proceed with the steps below once they confirm the build is done.

## Steps

1. Read `CLAUDE.md` to find the project name. Use the title from the "Project Overview" section (e.g. "Crappy Turd 2000"). Convert it to a filename-safe slug by lowercasing and replacing spaces with hyphens (e.g. `crappy-turd-2000`).

2. Verify the build output exists and is recent using PowerShell:

```powershell
$buildDir = "C:\Projects\Crappy Turd 2000\Builds\WebGL"
if (Test-Path "$buildDir\index.html") {
    Write-Output "Build found. Last modified: $((Get-Item $buildDir).LastWriteTime)"
} else {
    Write-Output "No build found at $buildDir"
}
```

If no build is found, tell the user to run the build from Unity first.

3. Once the build is confirmed, zip it with a version timestamp using PowerShell:

```powershell
$slug      = "<project-slug>"   # e.g. crappy-turd-2000
$timestamp = Get-Date -Format "yyyy-MM-dd_HHmm"
$buildDir  = "C:\Projects\Crappy Turd 2000\Builds\WebGL"
$zipDir    = "C:\Projects\Crappy Turd 2000\Builds\releases"
$zipPath   = "$zipDir\$slug-$timestamp.zip"

New-Item -ItemType Directory -Force $zipDir | Out-Null
Compress-Archive -Path "$buildDir\*" -DestinationPath $zipPath
Write-Output "Zipped to: $zipPath"
```

Replace `<project-slug>` and the project root path with the actual values derived from CLAUDE.md.

5. List the existing releases so the user can see their build history:

```powershell
Get-ChildItem "C:\Projects\Crappy Turd 2000\Builds\releases" | Sort-Object LastWriteTime -Descending | Select-Object Name, LastWriteTime | Format-Table -AutoSize
```

6. Report the zip path to the user and confirm the build is ready to upload to itch.io.
