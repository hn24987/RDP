[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../desktop/RdpLauncher/RdpLauncher.csproj'),
    [string]$GitRef = $env:GITHUB_REF,
    [string]$GithubOutput
)

function Test-ReleaseVersion {
    param([string]$Version)
    $match = [regex]::Match($Version, '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')
    if (-not $match.Success) { return $false }
    $prerelease = $match.Groups[4].Value
    foreach ($part in ($prerelease -split '\.')) {
        if ($part -match '^[0-9]+$' -and $part.Length -gt 1 -and $part.StartsWith('0')) { return $false }
    }
    return $true
}

function Resolve-BuildVersion {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ProjectPath, [string]$GitRef = '')
    [xml]$project = Get-Content -LiteralPath $ProjectPath -Raw -ErrorAction Stop
    $versions = @($project.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($versions.Count -ne 1 -or -not (Test-ReleaseVersion ([string]$versions[0]))) {
        throw 'The project must contain exactly one literal SemVer <Version> (for example, 1.2.0).'
    }
    $version = [string]$versions[0]
    $source = 'csproj'
    if ($GitRef.StartsWith('refs/tags/', [StringComparison]::Ordinal)) {
        $tag = $GitRef.Substring('refs/tags/'.Length)
        if (-not $tag.StartsWith('v', [StringComparison]::Ordinal) -or -not (Test-ReleaseVersion $tag.Substring(1))) {
            throw 'Release tags must use v followed by a valid SemVer version (for example, v1.2.0).'
        }
        $tagVersion = $tag.Substring(1)
        if ($tagVersion -cne $version) {
            throw "Tag version '$tagVersion' does not match project version '$version'. Update the project before tagging."
        }
        $version = $tagVersion
        $source = 'tag'
    }
    [pscustomobject]@{ Version = $version; Tag = "v$version"; Source = $source }
}

if ($MyInvocation.InvocationName -ne '.') {
    $result = Resolve-BuildVersion -ProjectPath $ProjectPath -GitRef $GitRef
    if ($GithubOutput) {
        "version=$($result.Version)" | Out-File -LiteralPath $GithubOutput -Append -Encoding utf8
        "tag=$($result.Tag)" | Out-File -LiteralPath $GithubOutput -Append -Encoding utf8
    }
    Write-Host "Build version: $($result.Version) ($($result.Source))"
}
