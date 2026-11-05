[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$IsccPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-IsccPath {
    param(
        [AllowNull()]
        [string]$ExplicitPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidate = if (Test-Path -LiteralPath $ExplicitPath -PathType Container) {
            Join-Path $ExplicitPath 'ISCC.exe'
        }
        else {
            $ExplicitPath
        }

        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }

        throw "ISCC.exe was not found at the explicitly supplied path '$ExplicitPath'."
    }

    $candidates = [System.Collections.Generic.List[string]]::new()

    $pathCommand = Get-Command 'ISCC.exe' -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -ne $pathCommand) {
        $candidates.Add($pathCommand.Source)
    }

    $uninstallRegistryPaths = @(
        'Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
        'Registry::HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
        'Registry::HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1'
    )

    foreach ($registryPath in $uninstallRegistryPaths) {
        if (-not (Test-Path -LiteralPath $registryPath)) {
            continue
        }

        $registryEntry = Get-ItemProperty -LiteralPath $registryPath
        $installLocationProperty = $registryEntry.PSObject.Properties['InstallLocation']
        if ($null -ne $installLocationProperty -and
            -not [string]::IsNullOrWhiteSpace([string]$installLocationProperty.Value)) {
            $candidates.Add((Join-Path ([string]$installLocationProperty.Value) 'ISCC.exe'))
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $candidates.Add((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'))
    }

    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'))
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates.Add((Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'))
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw @'
Inno Setup 7 compiler (ISCC.exe) was not found. Install Inno Setup 7 for the current user or all users, add ISCC.exe to PATH, or pass its path explicitly:
  .\installer\build-installer.ps1 -IsccPath "C:\path\to\ISCC.exe"
'@
}

function Reset-BuildDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPrefix = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]@('\', '/')) +
        [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset build directory outside the repository: '$fullPath'."
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repositoryRoot 'src\BezBukv.App\BezBukv.App.csproj'
$installerScriptPath = Join-Path $PSScriptRoot 'BezBukv.iss'
$setupIconPath = Join-Path $repositoryRoot 'assets\app.ico'
$publishDirectory = Join-Path $PSScriptRoot 'publish\win-x64'
$outputDirectory = Join-Path $PSScriptRoot 'Output'

foreach ($requiredFile in @($projectPath, $installerScriptPath, $setupIconPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required file was not found: '$requiredFile'."
    }
}

$resolvedIsccPath = Resolve-IsccPath -ExplicitPath $IsccPath

[xml]$projectXml = Get-Content -Raw -LiteralPath $projectPath
$versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "The <Version> property was not found in '$projectPath'."
}

$appVersion = $versionNode.InnerText.Trim()
if ($appVersion.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "The application version '$appVersion' contains characters that are invalid in a file name."
}

Reset-BuildDirectory -Path $publishDirectory -RepositoryRoot $repositoryRoot
Reset-BuildDirectory -Path $outputDirectory -RepositoryRoot $repositoryRoot

Write-Host "Publishing BezBukv $appVersion for win-x64 (Release, self-contained)..."
& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    --nologo `
    -p:PublishTrimmed=false
$publishExitCode = $LASTEXITCODE

if ($publishExitCode -ne 0) {
    throw "dotnet publish failed with exit code $publishExitCode. Installer compilation was not started."
}

$publishedExecutable = Join-Path $publishDirectory 'BezBukv.App.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "dotnet publish reported success, but '$publishedExecutable' was not created."
}

Write-Host "Compiling the Inno Setup installer with '$resolvedIsccPath'..."
& $resolvedIsccPath `
    "/DAppVersion=$appVersion" `
    "/DPublishDir=$publishDirectory" `
    "/DSetupIconFile=$setupIconPath" `
    "/O$outputDirectory" `
    $installerScriptPath
$compilerExitCode = $LASTEXITCODE

if ($compilerExitCode -ne 0) {
    throw "Inno Setup compilation failed with exit code $compilerExitCode."
}

$installerPath = Join-Path $outputDirectory "BezBukv-Setup-$appVersion.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Inno Setup reported success, but '$installerPath' was not created."
}

$installerFile = Get-Item -LiteralPath $installerPath
Write-Host ''
Write-Host 'Installer created successfully.'
Write-Host "Path: $($installerFile.FullName)"
Write-Host "Version: $appVersion"
Write-Host "Size: $($installerFile.Length) bytes"
