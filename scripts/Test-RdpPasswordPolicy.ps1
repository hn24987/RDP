$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This integration check requires Windows.' }

# Execute the policy block from the shipped workflow itself, so this checks the
# configuration that the RDP runner uses rather than a duplicate policy fixture.
$workflow = Get-Content (Join-Path $PSScriptRoot '../.github/workflows/main.yml') -Raw
$start = $workflow.IndexOf('          $policy = Join-Path')
$end = $workflow.IndexOf('          $plainPassword =', $start)
if ($start -lt 0 -or $end -le $start) { throw 'RDP password policy block was not found.' }
$block = $workflow.Substring($start, $end - $start) -replace '(?m)^          ', ''
& ([scriptblock]::Create($block))

$name = 'RdpPwdTest' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
try {
    foreach ($value in @('123456', 'a')) {
        $password = ConvertTo-SecureString $value -AsPlainText -Force
        New-LocalUser -Name $name -Password $password -AccountNeverExpires | Out-Null
        if (-not (Get-LocalUser -Name $name).Enabled) { throw 'Test account is not enabled.' }
        Remove-LocalUser -Name $name
    }
    Write-Host 'PASS: Windows accepts the default and a one-character custom password.'
}
finally {
    if (Get-LocalUser -Name $name -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $name }
}
