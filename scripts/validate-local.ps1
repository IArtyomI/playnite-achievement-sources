[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Achievement Sources must be validated on Windows because it targets .NET Framework and the Playnite desktop SDK."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repositoryRoot "AchievementSources.sln"
$projectDirectory = Join-Path $repositoryRoot "src\PlayniteAchievementSources"
$outputDirectory = Join-Path $projectDirectory "bin\$Configuration\net462"
$assemblyPath = Join-Path $outputDirectory "PlayniteAchievementSources.dll"
$manifestPath = Join-Path $outputDirectory "extension.yaml"
$vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"

function Resolve-MSBuild {
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    if (Test-Path $vswherePath) {
        $candidate = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1

        if ($candidate) {
            return $candidate
        }
    }

    throw "MSBuild was not found. Install Visual Studio 2022 or Build Tools with .NET Framework 4.6.2 targeting support."
}

function Resolve-VSTest {
    $command = Get-Command vstest.console.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    if (Test-Path $vswherePath) {
        $patterns = @(
            "Common7\IDE\Extensions\TestPlatform\vstest.console.exe",
            "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
        )

        foreach ($pattern in $patterns) {
            $candidate = & $vswherePath -latest -products * -find $pattern |
                Select-Object -First 1

            if ($candidate) {
                return $candidate
            }
        }
    }

    throw "Visual Studio Test Platform was not found. Modify the Visual Studio Build Tools installation and add the Testing tools core features component."
}

if (-not (Test-Path $solutionPath)) {
    throw "Solution not found: $solutionPath"
}

$runningPlayniteProcesses = @(Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessName -like "Playnite*" })

if ($runningPlayniteProcesses.Count -gt 0) {
    $processSummary = ($runningPlayniteProcesses |
        Sort-Object ProcessName, Id |
        ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ", "

    throw "Playnite is still running and has the development plugin DLL loaded: $processSummary. Exit Playnite completely, including its system-tray process, then run this script again."
}

$msbuildPath = Resolve-MSBuild

Push-Location $repositoryRoot
try {
    if (-not $SkipClean) {
        try {
            Get-ChildItem -Path (Join-Path $repositoryRoot "src"), (Join-Path $repositoryRoot "tests") `
                -Directory -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -in @("bin", "obj") } |
                Remove-Item -Recurse -Force -ErrorAction Stop
        }
        catch [System.UnauthorizedAccessException] {
            throw "A build output file is locked. Exit Playnite completely and close any tool inspecting the plugin output directory, then run validation again. Original error: $($_.Exception.Message)"
        }
    }

    Write-Host "Building $Configuration with $msbuildPath"
    & $msbuildPath $solutionPath /restore /m /t:Rebuild /p:Configuration=$Configuration /v:minimal

    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path $assemblyPath)) {
        throw "Expected plugin assembly was not produced: $assemblyPath"
    }

    if (-not (Test-Path $manifestPath)) {
        throw "Expected Playnite extension manifest was not produced: $manifestPath"
    }

    $testAssemblies = @(Get-ChildItem -Path (Join-Path $repositoryRoot "tests") -Filter *.Tests.dll -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -like "*\bin\$Configuration\net462\*" -and
            $_.FullName -notlike "*\ref\*"
        })

    if ($testAssemblies.Count -gt 0) {
        $vstestPath = Resolve-VSTest
        foreach ($testAssembly in $testAssemblies) {
            Write-Host "Running tests: $($testAssembly.FullName)"
            & $vstestPath $testAssembly.FullName /Logger:Console /TestCaseFilter:"Category!=Manual"

            if ($LASTEXITCODE -ne 0) {
                throw "Tests failed for $($testAssembly.FullName)."
            }
        }
    }

    Write-Host ""
    Write-Host "Local validation passed."
    Write-Host "Plugin output: $outputDirectory"
    Write-Host "Next: start Playnite and complete the manual load checklist in docs/local-validation.md."
}
finally {
    Pop-Location
}
