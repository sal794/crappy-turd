# Build WebGL for itch.io

Zips the latest WebGL build and deploys it to the game's itch.io page. Works for any game registered in `~/.claude/itch-games.json`.

Note: WebGL builds take several minutes and cannot be triggered programmatically due to tool timeouts. Instruct the user to build first via **Unity → File → Build Settings → Build**, then proceed once they confirm the build is done.

## Steps

1. Read the game registry and find the entry matching the current project:

```powershell
$registry = Get-Content "$env:USERPROFILE\.claude\itch-games.json" | ConvertFrom-Json
$cwd = (Get-Location).Path
$game = $registry | Where-Object { $cwd -like "$($_.projectRoot)*" } | Select-Object -First 1

if (-not $game) {
    Write-Output "No itch.io game registered for this project. Add an entry to ~/.claude/itch-games.json."
    exit 1
}

Write-Output "Found game: $($game.slug) -> $($game.itchTarget)"
Write-Output "Build dir:  $($game.projectRoot)\$($game.buildDir)"
```

If no match is found, tell the user to add their game to `~/.claude/itch-games.json` using the same format as the existing entries, then stop.

2. Verify the build output exists and is recent:

```powershell
$buildPath = "$($game.projectRoot)\$($game.buildDir)"
if (Test-Path "$buildPath\index.html") {
    Write-Output "Build found. Last modified: $((Get-Item $buildPath).LastWriteTime)"
} else {
    Write-Output "No build found at $buildPath"
}
```

If no build is found, tell the user to run the build from Unity first and stop here.

3. Zip the build, replacing any previous zip for this game:

```powershell
$releasesPath = "$($game.projectRoot)\$($game.releasesDir)"
$zipPath      = "$releasesPath\$($game.slug).zip"

New-Item -ItemType Directory -Force $releasesPath | Out-Null
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$buildPath\*" -DestinationPath $zipPath
Write-Output "Zipped to: $zipPath"
```

4. Deploy to itch.io using butler:

```powershell
& "C:\Users\samlu\bin\butler.exe" push $buildPath $game.itchTarget
```

Butler streams progress as it uploads. Wait for it to complete and confirm success. If it fails, report the full output to the user.

5. Report the zip path and confirm the build is live at the game's itch.io page (`https://iamthemeatball.itch.io/<game-slug>`).
