[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [switch]$SkipDistBuild,

    [switch]$ForceBackend,

    [switch]$ForceModels,

    [switch]$ForceRestore,

    [switch]$PruneBackendBuildDirectory,

    [string]$BackendGenerator,

    [string]$ModelArchive,

    [string]$ModelDownloadUrl,

    [string]$Version,

    [ValidateSet("x64", "x86")]
    [string[]]$Architecture = @("x64", "x86"),

    [string]$DistDir,

    [string]$OutputDir,

    [string]$ProjectDir,

    [string]$EnigmaConsolePath,

    [string]$EnigmaLicensePath,

    [switch]$CompressFiles
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$buildAllScript = Join-Path $scriptRoot "build-all.ps1"

function Resolve-FullPath {
    param(
        [string]$Path,
        [string]$BasePath
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Test-PathUnderRoot {
    param(
        [string]$Path,
        [string]$Root
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $normalizedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    return $normalizedPath.Equals($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalizedPath.StartsWith($normalizedRoot + "\", [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-EnigmaConsolePath {
    param([string]$ExplicitPath)

    $explicit = Resolve-FullPath -Path $ExplicitPath -BasePath $repoRoot
    if ($explicit) {
        if (-not (Test-Path -LiteralPath $explicit -PathType Leaf)) {
            throw "Enigma Virtual Box console was not found: $explicit"
        }

        return $explicit
    }

    $command = Get-Command enigmavbconsole.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidateRoots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
    foreach ($candidateRoot in $candidateRoots) {
        $candidate = Join-Path $candidateRoot "Enigma Virtual Box\enigmavbconsole.exe"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw "enigmavbconsole.exe was not found. Install Enigma Virtual Box or pass -EnigmaConsolePath."
}

function Resolve-EnigmaLicensePath {
    param(
        [string]$ExplicitPath,
        [string]$ConsolePath
    )

    $explicit = Resolve-FullPath -Path $ExplicitPath -BasePath $repoRoot
    if ($explicit) {
        if (-not (Test-Path -LiteralPath $explicit -PathType Leaf)) {
            throw "Enigma Virtual Box license was not found: $explicit"
        }

        return $explicit
    }

    $candidate = Join-Path (Split-Path -Parent $ConsolePath) "License.txt"
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return $candidate
    }

    $repoCopy = Join-Path $repoRoot "packaging\windows\EnigmaVirtualBox.LICENSE.txt"
    if (Test-Path -LiteralPath $repoCopy -PathType Leaf) {
        return $repoCopy
    }

    throw "Enigma Virtual Box license text was not found. Pass -EnigmaLicensePath."
}

function Add-SwitchArgument {
    param(
        [hashtable]$Arguments,
        [string]$Name,
        [bool]$Enabled
    )

    if ($Enabled) {
        $Arguments[$Name] = $true
    }
}

function Add-StringArgument {
    param(
        [hashtable]$Arguments,
        [string]$Name,
        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        $Arguments[$Name] = $Value
    }
}

function Get-UniqueArchitectures {
    $result = @()
    foreach ($item in $Architecture) {
        if ($result -notcontains $item) {
            $result += $item
        }
    }

    return $result
}

function Assert-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing ${Description}: $Path"
    }
}

function Remove-FileIfExists {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    $attemptCount = 20
    for ($attempt = 1; $attempt -le $attemptCount; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq $attemptCount) {
                throw "Could not remove existing file after $attemptCount attempts: $Path. Close any running copy or process that is using this file, then rerun the command. $($_.Exception.Message)"
            }

            Start-Sleep -Milliseconds 500
        }
    }
}

function ConvertTo-EvbBoolean {
    param([bool]$Value)

    if ($Value) { return "true" }
    return "false"
}

function Escape-EvbText {
    param([string]$Value)

    return [System.Security.SecurityElement]::Escape($Value)
}

function Test-ExcludedPath {
    param(
        [string]$Path,
        [string[]]$ExcludedPaths
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    foreach ($excludedPath in $ExcludedPaths) {
        $normalizedExcludedPath = [System.IO.Path]::GetFullPath($excludedPath).TrimEnd('\')
        if ($normalizedPath.Equals($normalizedExcludedPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-EvbChildSortBucket {
    param(
        [System.IO.FileSystemInfo]$Child,
        [string]$RootPath
    )

    $root = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\')
    $fullPath = [System.IO.Path]::GetFullPath($Child.FullName).TrimEnd('\')
    $relativePath = $fullPath.Substring($root.Length).TrimStart('\')

    if ($Child.PSIsContainer) {
        if ($relativePath -eq "engine") {
            return 900
        }

        if ($relativePath -eq "licenses") {
            return 910
        }

        return 800
    }

    switch -Exact ($Child.Name) {
        "Real-ESRGAN GUI.exe" { return 0 }
        "Real-ESRGAN GUI.dll" { return 1 }
        "Real-ESRGAN GUI.deps.json" { return 2 }
        "Real-ESRGAN GUI.runtimeconfig.json" { return 3 }
        "hostfxr.dll" { return 10 }
        "hostpolicy.dll" { return 11 }
        "coreclr.dll" { return 12 }
        "System.Private.CoreLib.dll" { return 13 }
        "clrjit.dll" { return 14 }
        "PresentationFramework.dll" { return 20 }
        "PresentationCore.dll" { return 21 }
        "WindowsBase.dll" { return 22 }
        "System.Xaml.dll" { return 23 }
        default {
            if ($Child.Extension.Equals(".dll", [System.StringComparison]::OrdinalIgnoreCase)) {
                return 100
            }

            return 200
        }
    }
}

function New-EvbFileEntry {
    param(
        [string]$Name,
        [string]$Path
    )

    $escapedName = Escape-EvbText -Value $Name
    $escapedPath = Escape-EvbText -Value $Path

    return @"
<File>
    <Type>2</Type>
    <Name>$escapedName</Name>
    <File>$escapedPath</File>
    <ActiveX>false</ActiveX>
    <ActiveXInstall>false</ActiveXInstall>
    <Action>0</Action>
    <OverwriteDateTime>false</OverwriteDateTime>
    <OverwriteAttributes>false</OverwriteAttributes>
    <PassCommandLine>false</PassCommandLine>
</File>
"@
}

function New-EvbDirectoryEntry {
    param(
        [System.IO.DirectoryInfo]$Directory,
        [string]$RootPath,
        [string[]]$ExcludedPaths
    )

    $escapedName = Escape-EvbText -Value $Directory.Name
    $entries = New-EvbEntriesForDirectory -DirectoryPath $Directory.FullName -RootPath $RootPath -ExcludedPaths $ExcludedPaths

    return @"
<File>
    <Type>3</Type>
    <Name>$escapedName</Name>
    <Action>0</Action>
    <OverwriteDateTime>false</OverwriteDateTime>
    <OverwriteAttributes>false</OverwriteAttributes>
    <Files>
$entries
    </Files>
</File>
"@
}

function New-EvbEntriesForDirectory {
    param(
        [string]$DirectoryPath,
        [string]$RootPath,
        [string[]]$ExcludedPaths
    )

    $entries = New-Object System.Collections.Generic.List[string]
    $children = Get-ChildItem -LiteralPath $DirectoryPath -Force |
        Sort-Object @{ Expression = { Get-EvbChildSortBucket -Child $_ -RootPath $RootPath } }, Name

    foreach ($child in $children) {
        if (Test-ExcludedPath -Path $child.FullName -ExcludedPaths $ExcludedPaths) {
            continue
        }

        if ($child.PSIsContainer) {
            $entries.Add((New-EvbDirectoryEntry -Directory $child -RootPath $RootPath -ExcludedPaths $ExcludedPaths))
        }
        else {
            $entries.Add((New-EvbFileEntry -Name $child.Name -Path $child.FullName))
        }
    }

    return ($entries -join [Environment]::NewLine)
}

function New-EvbProjectContent {
    param(
        [string]$InputFile,
        [string]$OutputFile,
        [string]$FileEntries,
        [bool]$CompressVirtualFiles
    )

    $escapedInputFile = Escape-EvbText -Value $InputFile
    $escapedOutputFile = Escape-EvbText -Value $OutputFile
    $deleteExtractedOnExit = ConvertTo-EvbBoolean -Value $true
    $compressFilesValue = ConvertTo-EvbBoolean -Value $CompressVirtualFiles

    return @"
<?xml encoding="utf-16"?>
<>
    <InputFile>$escapedInputFile</InputFile>
    <OutputFile>$escapedOutputFile</OutputFile>
    <Files>
        <Enabled>true</Enabled>
        <DeleteExtractedOnExit>$deleteExtractedOnExit</DeleteExtractedOnExit>
        <CompressFiles>$compressFilesValue</CompressFiles>
        <Files>
            <File>
                <Type>3</Type>
                <Name>%DEFAULT FOLDER%</Name>
                <Action>0</Action>
                <OverwriteDateTime>false</OverwriteDateTime>
                <OverwriteAttributes>false</OverwriteAttributes>
                <Files>
$FileEntries
                </Files>
            </File>
        </Files>
    </Files>
    <Registries>
        <Enabled>true</Enabled>
        <Registries/>
    </Registries>
    <Packaging>
        <Enabled>false</Enabled>
    </Packaging>
    <Options>
        <ShareVirtualSystem>true</ShareVirtualSystem>
        <MapExecutableWithTemporaryFile>true</MapExecutableWithTemporaryFile>
        <AllowRunningOfVirtualExeFiles>true</AllowRunningOfVirtualExeFiles>
    </Options>
</>
"@
}

$resolvedOutputDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputDir)) { "artifacts\portable-enigma" } else { $OutputDir }) -BasePath $repoRoot
$resolvedProjectDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($ProjectDir)) { "artifacts\intermediate\enigma-projects" } else { $ProjectDir }) -BasePath $repoRoot
$architectures = @(Get-UniqueArchitectures)
if (-not [string]::IsNullOrWhiteSpace($DistDir) -and $architectures.Count -ne 1) {
    throw "-DistDir can only be used with a single -Architecture value. Pass -Architecture x64 or -Architecture x86, or omit -DistDir for the default artifacts\intermediate\portable\<arch> layout."
}

$enigmaConsole = Resolve-EnigmaConsolePath -ExplicitPath $EnigmaConsolePath
$enigmaLicense = Resolve-EnigmaLicensePath -ExplicitPath $EnigmaLicensePath -ConsolePath $enigmaConsole
Write-Host "Using Enigma Virtual Box console: $enigmaConsole"
New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $resolvedProjectDir | Out-Null

$outputs = New-Object System.Collections.Generic.List[object]

for ($index = 0; $index -lt $architectures.Count; $index++) {
    $arch = $architectures[$index]
    $step = $index + 1
    Write-Host ""
    Write-Host "========== Enigma $step/$($architectures.Count): $arch =========="

    $defaultDistDir = Join-Path (Join-Path (Join-Path "artifacts" "intermediate") "portable") $arch
    $resolvedDistDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($DistDir)) { $defaultDistDir } else { $DistDir }) -BasePath $repoRoot
    $outputPath = Join-Path $resolvedOutputDir "Real-ESRGAN-GUI-Portable-$arch.exe"
    $projectPath = Join-Path $resolvedProjectDir "Real-ESRGAN-GUI-Portable-$arch.evb"

    if (Test-PathUnderRoot -Path $outputPath -Root $resolvedDistDir) {
        throw "Enigma output must not be placed inside the portable source directory: $outputPath"
    }

    if (-not $SkipDistBuild) {
        $buildArgs = @{
            Configuration = $Configuration
            Architecture = $arch
            OutputDir = $resolvedDistDir
        }
        Add-SwitchArgument -Arguments $buildArgs -Name "Clean" -Enabled $Clean
        Add-SwitchArgument -Arguments $buildArgs -Name "ForceBackend" -Enabled $ForceBackend
        Add-SwitchArgument -Arguments $buildArgs -Name "ForceModels" -Enabled $ForceModels
        Add-SwitchArgument -Arguments $buildArgs -Name "ForceRestore" -Enabled $ForceRestore
        Add-SwitchArgument -Arguments $buildArgs -Name "PruneBackendBuildDirectory" -Enabled $PruneBackendBuildDirectory
        Add-StringArgument -Arguments $buildArgs -Name "BackendGenerator" -Value $BackendGenerator
        Add-StringArgument -Arguments $buildArgs -Name "ModelArchive" -Value $ModelArchive
        Add-StringArgument -Arguments $buildArgs -Name "ModelDownloadUrl" -Value $ModelDownloadUrl
        Add-StringArgument -Arguments $buildArgs -Name "Version" -Value $Version

        & $buildAllScript @buildArgs
    }
    else {
        Write-Host "Skipping $arch portable output build because -SkipDistBuild was specified."
    }

    $inputFile = Join-Path $resolvedDistDir "Launcher.exe"
    $wpfFile = Join-Path $resolvedDistDir "Real-ESRGAN GUI.exe"
    $backendFile = Join-Path $resolvedDistDir "engine\realesrgan-ncnn-vulkan.exe"
    $architectureMarkerPath = Join-Path $resolvedDistDir "ARCHITECTURE.txt"
    $packageKindMarkerPath = Join-Path $resolvedDistDir "PACKAGE_KIND.txt"
    $thirdPartyNoticePath = Join-Path $resolvedDistDir "THIRD_PARTY_NOTICES.md"

    Assert-RequiredFile -Path $inputFile -Description "$arch Launcher.exe"
    Assert-RequiredFile -Path $wpfFile -Description "$arch Real-ESRGAN GUI.exe"
    Assert-RequiredFile -Path $backendFile -Description "$arch backend executable"
    Assert-RequiredFile -Path $architectureMarkerPath -Description "$arch ARCHITECTURE.txt"
    Assert-RequiredFile -Path $packageKindMarkerPath -Description "$arch PACKAGE_KIND.txt"
    Assert-RequiredFile -Path $thirdPartyNoticePath -Description "$arch THIRD_PARTY_NOTICES.md"

    $distArchitecture = (Get-Content -LiteralPath $architectureMarkerPath -TotalCount 1).Trim()
    if ($distArchitecture -ne $arch) {
        throw "Portable output architecture is '$distArchitecture', expected '$arch': $resolvedDistDir"
    }

    $enigmaLicenseTargetDir = Join-Path $resolvedDistDir "licenses"
    New-Item -ItemType Directory -Force -Path $enigmaLicenseTargetDir | Out-Null
    Copy-Item -LiteralPath $enigmaLicense -Destination (Join-Path $enigmaLicenseTargetDir "Enigma-Virtual-Box-LICENSE.txt") -Force

    if ($Clean) {
        foreach ($pathToRemove in @($outputPath, $projectPath)) {
            Remove-FileIfExists -Path $pathToRemove
        }
    }

    Remove-FileIfExists -Path $outputPath

    Write-Host "Generating Enigma intermediate project: $projectPath"
    Write-Host "Ordering virtual files for cold startup: WPF app/runtime first, backend models later."
    $fileEntries = New-EvbEntriesForDirectory -DirectoryPath $resolvedDistDir -RootPath $resolvedDistDir -ExcludedPaths @($inputFile)
    $projectContent = New-EvbProjectContent `
        -InputFile $inputFile `
        -OutputFile $outputPath `
        -FileEntries $fileEntries `
        -CompressVirtualFiles $CompressFiles

    [System.IO.File]::WriteAllText($projectPath, $projectContent, [System.Text.Encoding]::Unicode)

    Write-Host "Enigma Virtual Box options:"
    Write-Host "  Enable Files Virtualization      : true"
    Write-Host "  Delete Extracted On Exit         : true"
    Write-Host "  Enable Registry Virtualization   : true"
    Write-Host "  Allow Writing to Virtual Registry: true"
    Write-Host "  Share Virtual System             : true"
    Write-Host "  Allow Running Virtual EXE Files  : true"

    Write-Host "Building Enigma portable executable..."
    & $enigmaConsole $projectPath
    if ($LASTEXITCODE -ne 0) {
        throw "Enigma Virtual Box failed with exit code $LASTEXITCODE."
    }

    Assert-RequiredFile -Path $outputPath -Description "$arch Enigma portable executable"

    $inputSize = (Get-Item -LiteralPath $inputFile).Length
    $outputSize = (Get-Item -LiteralPath $outputPath).Length
    if ($outputSize -le $inputSize) {
        throw "Enigma output is not larger than the launcher input. Output may be incomplete: $outputPath"
    }

    $outputs.Add([pscustomobject]@{
        Architecture = $arch
        Portable = $resolvedDistDir
        EnigmaPortable = $outputPath
        Project = $projectPath
    })
}

Write-Host ""
Write-Host "========== Enigma Portable Artifacts Complete =========="
foreach ($output in $outputs) {
    Write-Host "Architecture: $($output.Architecture)"
    Write-Host "  Portable source     : $($output.Portable)"
    Write-Host "  Enigma portable     : $($output.EnigmaPortable)"
    Write-Host "  Intermediate project: $($output.Project)"
}
