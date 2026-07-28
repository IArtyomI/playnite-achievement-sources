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

function Resolve-MSBuild {
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        $candidate = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1

        if ($candidate) {
            return $candidate
        }
    }

    throw "MSBuild was not found. Install Visual Studio 2022 or Build Tools with .NET Framework 4.6.2 targeting support."
}

function Get-RunningPlayniteProcesses {
    return @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like "Playnite*" })
}

if (-not (Test-Path $solutionPath)) {
    throw "Solution not found: $solutionPath"
}

$runningPlayniteProcesses = Get-RunningPlayniteProcesses
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

    $testProjects = Get-ChildItem -Path (Join-Path $repositoryRoot "tests") -Filter *.csproj -Recurse -ErrorAction SilentlyContinue
    foreach ($testProject in $testProjects) {
        Write-Host "Running tests: $($testProject.FullName)"
        dotnet test $testProject.FullName --configuration $Configuration --no-restore

        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed for $($testProject.FullName)."
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
