$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Get-BuildVersion.ps1')
$folder = Join-Path ([IO.Path]::GetTempPath()) ('rdp-version-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder | Out-Null
$project = Join-Path $folder 'App.csproj'
$passed = 0
function Assert-Equal($actual, $expected) { if ($actual -cne $expected) { throw "Expected '$expected', got '$actual'." } }
function Write-Project([string]$version) { "<Project><PropertyGroup><Version>$version</Version></PropertyGroup></Project>" | Set-Content $project }
function Expect-Rejection([scriptblock]$action) { $rejected = $false; try { & $action | Out-Null } catch { $rejected = $true }; if (-not $rejected) { throw 'Invalid version was accepted.' } }
try {
    Write-Project '1.2.3'
    Assert-Equal (Resolve-BuildVersion $project '').Version '1.2.3'; $passed++
    Assert-Equal (Resolve-BuildVersion $project 'refs/pull/9/merge').Source 'csproj'; $passed++
    Assert-Equal (Resolve-BuildVersion $project 'refs/tags/v1.2.3').Source 'tag'; $passed++
    Expect-Rejection { Resolve-BuildVersion $project 'refs/tags/v1.2.4' }; $passed++
    Expect-Rejection { Resolve-BuildVersion $project 'refs/tags/release-1.2.3' }; $passed++
    foreach ($invalid in @('1.2', '01.2.3', '1.2.3-01', '1.2.3+bad value', '$(Injected)', '1.2.3/path', '1.2.3%0Avalue')) {
        Write-Project $invalid
        Expect-Rejection { Resolve-BuildVersion $project '' }; $passed++
    }
    Write-Project '2.0.0-rc.1+build.3'
    Assert-Equal (Resolve-BuildVersion $project 'refs/tags/v2.0.0-rc.1+build.3').Version '2.0.0-rc.1+build.3'; $passed++
    Write-Host "$passed version-resolution tests passed."
} finally {
    Remove-Item -LiteralPath $folder -Recurse -Force
}
