# Builds Switch it up, installs it, and optionally publishes a GitHub release.
#
#   .\release.ps1                  build + install on this PC
#   .\release.ps1 -Version 1.1     also push to GitHub and publish release v1.1
#   .\release.ps1 -Version 1.1 -Notes "What changed"
param(
    [string]$Version,
    [string]$Notes = "Download Switch it up.exe and run it; no install needed."
)
$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$exeName = 'Switch it up.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Switch it up'

if ($Version) {
    # Check before doing anything, so a half-finished release can't happen.
    if (git -C $dir status --porcelain) { throw 'There are uncommitted changes. Commit them first, then run this again.' }
    git -C $dir fetch -q --tags origin
    if (git -C $dir tag --list "v$Version") { throw "Release v$Version already exists. Pick a new version number." }
}

Write-Host 'Building...'
& cmd /c "`"$dir\build.cmd`""
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Write-Host "Installing to $installDir ..."
$running = @(Get-Process 'Switch it up' -ErrorAction SilentlyContinue)
foreach ($p in $running) {
    $p.CloseMainWindow() | Out-Null
    if (-not $p.WaitForExit(5000)) { $p.Kill(); $p.WaitForExit() }
}
New-Item -ItemType Directory -Force $installDir | Out-Null
Copy-Item (Join-Path $dir $exeName) (Join-Path $installDir $exeName) -Force
if ($running.Count -gt 0) { Start-Process (Join-Path $installDir $exeName) }
Write-Host 'Installed.'

if ($Version) {
    $tag = "v$Version"
    Write-Host "Publishing $tag to GitHub..."
    Push-Location $dir
    try {
        git push -q origin main
        if ($LASTEXITCODE -ne 0) { throw 'git push failed.' }
        gh release create $tag (Join-Path $dir $exeName) --target main --title "Switch it up $tag" --notes $Notes
        if ($LASTEXITCODE -ne 0) { throw 'Creating the GitHub release failed.' }
    }
    finally { Pop-Location }
    Write-Host "Published $tag."
}
