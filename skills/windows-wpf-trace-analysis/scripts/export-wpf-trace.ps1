[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TracePath,

    [string]$OutputDirectory,

    [string[]]$Profiles = @(
        "DotNETRuntime",
        "AppLaunch",
        "XamlAppResponsivenessAnalysis"
    ),

    [ValidateSet("Single", "Separate")]
    [string]$Mode,

    [ValidateSet("CSV", "XML")]
    [string]$OutputFormat,

    [string]$Delimiter = ",",

    [string]$RangeStart,

    [string]$RangeEnd,

    [string[]]$Marks,

    [string[]]$RegionsXml,

    [string]$Region,

    [switch]$Symbols,

    [switch]$Tti,

    [string[]]$WpaExporterArgument = @(),

    [string[]]$XperfArgument = @(),

    [switch]$IncludeHtmlResponsiveness,

    [int]$TimeoutSeconds = 300,

    [switch]$SkipWpa,

    [switch]$SkipXperf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ExistingPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if (-not $resolved) {
        throw "$Description not found: $Path"
    }

    return $resolved.Path
}

function Resolve-WptTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $command = Get-Command $FileName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\Windows Performance Toolkit\$FileName"),
        (Join-Path $env:ProgramFiles "Windows Kits\10\Windows Performance Toolkit\$FileName")
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return $candidate
        }
    }

    throw "Windows Performance Toolkit tool not found: $FileName"
}

function Resolve-WpaProfile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolkitDirectory,

        [Parameter(Mandatory = $true)]
        [string]$ProfileName
    )

    $catalog = Join-Path $ToolkitDirectory "Catalog"
    $profileFiles = @{
        DotNETRuntime                  = "DotNETRuntime.wpaprofile"
        AppLaunch                      = "AppLaunch.wpaProfile"
        XamlAppResponsivenessAnalysis  = "XamlAppResponsivenessAnalysis.wpaprofile"
        HtmlResponsivenessAnalysis     = "HtmlResponsivenessAnalysis.wpaprofile"
    }

    if (-not $profileFiles.ContainsKey($ProfileName)) {
        throw "Unknown WPA profile '$ProfileName'. Known profiles: $($profileFiles.Keys -join ', ')"
    }

    return Resolve-ExistingPath -Path (Join-Path $catalog $profileFiles[$ProfileName]) -Description "WPA profile"
}

function Resolve-OutputPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function ConvertTo-CommandLineArgument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Argument
    )

    if ($Argument.Length -eq 0) {
        return '""'
    }

    if ($Argument -notmatch '[\s"]') {
        return $Argument
    }

    $escaped = $Argument -replace '(\\*)"', '$1$1\"'
    $escaped = $escaped -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}

function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [hashtable]$Environment = @{},

        [int]$TimeoutSeconds = 300
    )

    $stdoutPath = Join-Path $WorkingDirectory ("stdout-" + [guid]::NewGuid().ToString("N") + ".txt")
    $stderrPath = Join-Path $WorkingDirectory ("stderr-" + [guid]::NewGuid().ToString("N") + ".txt")
    $previousEnvironment = @{}

    try {
        foreach ($key in $Environment.Keys) {
            $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, "Process")
            [Environment]::SetEnvironmentVariable($key, [string]$Environment[$key], "Process")
        }

        $argumentLine = ($Arguments | ForEach-Object { ConvertTo-CommandLineArgument -Argument $_ }) -join " "

        $process = Start-Process -FilePath $FilePath `
            -ArgumentList $argumentLine `
            -WorkingDirectory $WorkingDirectory `
            -NoNewWindow `
            -PassThru `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath

        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try {
                Stop-Process -Id $process.Id -Force
            }
            catch {
                Write-Warning "Failed to kill timed-out process $($process.Id): $_"
            }

            throw "Timed out after $TimeoutSeconds seconds: $FilePath $($Arguments -join ' ')"
        }

        $process.Refresh()
        $exitCode = $process.ExitCode
    }
    finally {
        foreach ($key in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key], "Process")
        }
    }

    $capturedStdOut = if (Test-Path -LiteralPath $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw } else { "" }
    $capturedStdErr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { "" }
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue

    return [pscustomobject]@{
        ExitCode = $exitCode
        StdOut   = $capturedStdOut
        StdErr   = $capturedStdErr
    }
}

$trace = Resolve-ExistingPath -Path $TracePath -Description "ETL trace"
if (-not $OutputDirectory) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($trace)
    $OutputDirectory = Join-Path (Get-Location) "trace-analysis\$baseName"
}

$outputRoot = Resolve-OutputPath -Path $OutputDirectory
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

if ($RangeStart -and -not $RangeEnd) {
    throw "-RangeStart requires -RangeEnd."
}

if ($RangeEnd -and -not $RangeStart) {
    throw "-RangeEnd requires -RangeStart."
}

if ($Marks -and $Marks.Count -ne 2) {
    throw "-Marks requires exactly two marker names."
}

if ($Region -and (-not $RegionsXml -or $RegionsXml.Count -eq 0)) {
    throw "-Region requires at least one -RegionsXml manifest path."
}

$resolvedRegionsXml = @()
foreach ($manifest in $RegionsXml) {
    $resolvedRegionsXml += Resolve-ExistingPath -Path $manifest -Description "Regions XML manifest"
}

$logPath = Join-Path $outputRoot "export-log.txt"
"Trace: $trace" | Set-Content -LiteralPath $logPath -Encoding utf8
"Output: $outputRoot" | Add-Content -LiteralPath $logPath -Encoding utf8
"Started: $((Get-Date).ToString('o'))" | Add-Content -LiteralPath $logPath -Encoding utf8

if ($IncludeHtmlResponsiveness -and ($Profiles -notcontains "HtmlResponsivenessAnalysis")) {
    $Profiles += "HtmlResponsivenessAnalysis"
}

$wpaExporter = $null
$xperf = $null
if (-not $SkipWpa) {
    $wpaExporter = Resolve-WptTool -FileName "wpaexporter.exe"
}

if (-not $SkipXperf) {
    $xperf = Resolve-WptTool -FileName "xperf.exe"
}

if ($wpaExporter) {
    $toolkitDirectory = Split-Path -Parent $wpaExporter
    $localAppData = Join-Path $outputRoot "_localappdata"
    $tempDirectory = Join-Path $outputRoot "_temp"
    New-Item -ItemType Directory -Force -Path $localAppData, $tempDirectory | Out-Null

    $environment = @{
        LOCALAPPDATA = $localAppData
        TEMP         = $tempDirectory
        TMP          = $tempDirectory
    }

    $profileSlugs = @{
        DotNETRuntime                 = "dotnet"
        AppLaunch                     = "applaunch"
        XamlAppResponsivenessAnalysis = "xaml"
        HtmlResponsivenessAnalysis    = "html"
    }

    foreach ($profile in $Profiles) {
        $profilePath = Resolve-WpaProfile -ToolkitDirectory $toolkitDirectory -ProfileName $profile
        $profileOutput = Join-Path $outputRoot ("wpa-" + $profileSlugs[$profile])
        New-Item -ItemType Directory -Force -Path $profileOutput | Out-Null

        $wpaArguments = @(
            "-i", $trace,
            "-profile", $profilePath,
            "-outputfolder", $profileOutput,
            "-prefix", "$profile`_",
            "-delimiter", $Delimiter
        )

        if ($Mode) {
            $wpaArguments += @("-mode", $Mode)
        }

        if ($OutputFormat) {
            $wpaArguments += @("-outputformat", $OutputFormat)
        }

        if ($RangeStart -and $RangeEnd) {
            $wpaArguments += @("-range", $RangeStart, $RangeEnd)
        }

        if ($Marks) {
            $wpaArguments += @("-marks", $Marks[0], $Marks[1])
        }

        foreach ($manifest in $resolvedRegionsXml) {
            $wpaArguments += @("-regionsxml", $manifest)
        }

        if ($Region) {
            $wpaArguments += @("-region", $Region)
        }

        if ($Symbols) {
            $wpaArguments += "-symbols"
        }

        if ($Tti) {
            $wpaArguments += "-tti"
        }

        if ($WpaExporterArgument) {
            $wpaArguments += $WpaExporterArgument
        }

        "Exporting WPA profile: $profile" | Tee-Object -FilePath $logPath -Append
        $result = Invoke-Tool -FilePath $wpaExporter `
            -Arguments $wpaArguments `
            -WorkingDirectory $outputRoot `
            -Environment $environment `
            -TimeoutSeconds $TimeoutSeconds

        $result.StdOut | Add-Content -LiteralPath (Join-Path $profileOutput "stdout.txt") -Encoding utf8
        $result.StdErr | Add-Content -LiteralPath (Join-Path $profileOutput "stderr.txt") -Encoding utf8
        "WPA profile $profile exit code: $($result.ExitCode)" | Add-Content -LiteralPath $logPath -Encoding utf8

        if ($result.ExitCode -ne 0) {
            Write-Warning "wpaexporter exit code $($result.ExitCode) for profile $profile. See $profileOutput."
        }
    }
}

if ($xperf) {
    $actions = @(
        "tracestats",
        "process",
        "uidelay",
        "hardfault",
        "pagefault",
        "diskio",
        "cpudisk",
        "screenshots",
        "marks",
        "focuschange",
        "eventmetadata"
    )

    foreach ($action in $actions) {
        $outFile = Join-Path $outputRoot "xperf_$action.txt"
        $xperfArguments = @("-i", $trace, "-o", $outFile, "-a", $action)
        if ($Tti) {
            $xperfArguments += "-tti"
        }
        if ($XperfArgument) {
            $xperfArguments += $XperfArgument
        }

        "Exporting xperf action: $action" | Tee-Object -FilePath $logPath -Append
        $result = Invoke-Tool -FilePath $xperf `
            -Arguments $xperfArguments `
            -WorkingDirectory $outputRoot `
            -TimeoutSeconds $TimeoutSeconds

        if ($result.StdOut) {
            $result.StdOut | Add-Content -LiteralPath $outFile -Encoding utf8
        }
        if ($result.StdErr) {
            $result.StdErr | Add-Content -LiteralPath (Join-Path $outputRoot "xperf_$action.stderr.txt") -Encoding utf8
        }
        "xperf $action exit code: $($result.ExitCode)" | Add-Content -LiteralPath $logPath -Encoding utf8

        if ($result.ExitCode -ne 0) {
            Write-Warning "xperf exit code $($result.ExitCode) for action $action. See $outFile."
        }
    }
}

"Finished: $((Get-Date).ToString('o'))" | Add-Content -LiteralPath $logPath -Encoding utf8
Write-Host "Trace export complete: $outputRoot"
