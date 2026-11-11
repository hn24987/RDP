[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'win-arm64')][string]$Runtime,
    [string]$OutputDirectory,
    [string]$GitRef = $env:GITHUB_REF
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'desktop/RdpLauncher/RdpLauncher.csproj'
. (Join-Path $PSScriptRoot 'Get-BuildVersion.ps1') -ProjectPath $project -GitRef $GitRef
$build = Resolve-BuildVersion -ProjectPath $project -GitRef $GitRef
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root "dist/$Runtime" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$work = Join-Path $OutputDirectory ('.work-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $work 'app'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
try {
    & dotnet publish $project -c Release -r $Runtime --self-contained true `
        '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' `
        '-p:EnableCompressionInSingleFile=true' '-p:DebugType=None' "-p:Version=$($build.Version)" -o $payload
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Runtime (exit $LASTEXITCODE)." }
    $exe = Join-Path $payload 'RdpLauncher.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Publish output does not contain RdpLauncher.exe.' }
    # Verify the PE machine field. ARM64 is cross-built here, not executed.
    $stream = [IO.File]::OpenRead($exe)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'Publish output is not a PE executable.' }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 0 -or $peOffset -gt ($stream.Length - 6)) { throw 'Invalid PE header offset.' }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw 'Invalid PE header signature.' }
        $machine = $reader.ReadUInt16()
        $expectedMachine = if ($Runtime -eq 'win-arm64') { 0xAA64 } else { 0x8664 }
        if ($machine -ne $expectedMachine) { throw "EXE architecture does not match $Runtime." }
        Write-Host "Verified $Runtime PE machine field."
    } finally { $reader.Dispose(); $stream.Dispose() }
    foreach ($required in @('README.md', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $root $required) -Destination $payload -ErrorAction Stop
    }
    foreach ($optional in @('RELEASE_NOTES.md', 'RELEASE_PROMPT.md', 'CONTRIBUTING.md')) {
        $path = Join-Path $root $optional
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $payload }
    }
    $docs = Join-Path $root 'docs'
    if (Test-Path -LiteralPath $docs) { Copy-Item -LiteralPath $docs -Destination (Join-Path $payload 'docs') -Recurse }
    Copy-Item -LiteralPath (Join-Path $root '.github/workflows/main.yml') -Destination (Join-Path $payload 'main.yml')
    $exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    "$exeHash  RdpLauncher.exe" | Set-Content -LiteralPath (Join-Path $payload 'SHA256.txt') -Encoding utf8
    $zipName = "RDP-Launcher-v$($build.Version)-Windows-$($Runtime.Substring(4)).zip"
    $zipPath = Join-Path $OutputDirectory $zipName
    Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zipPath -Force
    $standaloneName = "RdpLauncher-$Runtime.exe"
    Copy-Item -LiteralPath $exe -Destination (Join-Path $OutputDirectory $standaloneName) -Force
    $checksums = @("$exeHash  $standaloneName")
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksums += "$zipHash  $zipName"
    if ($Runtime -eq 'win-x64') {
        Copy-Item -LiteralPath $exe -Destination (Join-Path $OutputDirectory 'RdpLauncher.exe') -Force
        $checksums += "$exeHash  RdpLauncher.exe"
    }
    $checksums | Set-Content -LiteralPath (Join-Path $OutputDirectory "SHA256-$Runtime.txt") -Encoding utf8
    Write-Host "Packaged ${Runtime}: $zipName"
} finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
