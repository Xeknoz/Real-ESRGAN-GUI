[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AssemblyPath,

    [string[]]$ForbiddenTypePatterns = @("PreviewDebug"),

    [string[]]$ForbiddenTextPatterns = $ForbiddenTypePatterns
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
    throw "Assembly does not exist: $AssemblyPath"
}

$resolvedAssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$assembly = [System.Reflection.Assembly]::LoadFile($resolvedAssemblyPath)

function Test-ContainsByteSequence {
    param(
        [byte[]]$Bytes,
        [byte[]]$Sequence
    )

    if ($Sequence.Length -eq 0 -or $Bytes.Length -lt $Sequence.Length) {
        return $false
    }

    $lastStart = $Bytes.Length - $Sequence.Length
    for ($index = 0; $index -le $lastStart; $index++) {
        $matches = $true
        for ($sequenceIndex = 0; $sequenceIndex -lt $Sequence.Length; $sequenceIndex++) {
            if ($Bytes[$index + $sequenceIndex] -ne $Sequence[$sequenceIndex]) {
                $matches = $false
                break
            }
        }

        if ($matches) {
            return $true
        }
    }

    return $false
}

$forbiddenTypes = @(
    $assembly.GetTypes() | Where-Object {
        $typeName = $_.FullName
        foreach ($pattern in $ForbiddenTypePatterns) {
            if ($typeName -like "*$pattern*") {
                return $true
            }
        }

        return $false
    } | Sort-Object FullName
)

if ($forbiddenTypes.Count -gt 0) {
    $typeList = ($forbiddenTypes | ForEach-Object { $_.FullName }) -join ", "
    throw "Release binary contains forbidden debug panel types: $typeList"
}

$assemblyBytes = [System.IO.File]::ReadAllBytes($resolvedAssemblyPath)
$forbiddenTexts = @(
    foreach ($pattern in $ForbiddenTextPatterns) {
        $utf8Pattern = [System.Text.Encoding]::UTF8.GetBytes($pattern)
        $utf16Pattern = [System.Text.Encoding]::Unicode.GetBytes($pattern)
        if ((Test-ContainsByteSequence -Bytes $assemblyBytes -Sequence $utf8Pattern) -or
            (Test-ContainsByteSequence -Bytes $assemblyBytes -Sequence $utf16Pattern)) {
            $pattern
        }
    }
)

if ($forbiddenTexts.Count -gt 0) {
    $textList = ($forbiddenTexts | Sort-Object -Unique) -join ", "
    throw "Release binary contains forbidden debug panel text: $textList"
}

Write-Host "Release binary debug panel check passed: $resolvedAssemblyPath"
