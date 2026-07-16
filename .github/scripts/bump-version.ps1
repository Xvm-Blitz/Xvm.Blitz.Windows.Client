param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("patch", "minor", "major")]
    [string] $Bump,

    [string] $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)

$ErrorActionPreference = "Stop"

function Write-Utf8NoBom {
    param(
        [string] $Path,
        [string] $Content
    )

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$propsPath = Join-Path $RepoRoot "Directory.Build.props"
$manifestPath = Join-Path $RepoRoot "src/Xvm.Blitz.Windows.Client.UI/app.manifest"
$wxsPath = Join-Path $RepoRoot "src/Xvm.Blitz.Windows.Client.Installer/Xvm Blitz.wxs"

$propsContent = Get-Content -Path $propsPath -Raw
if ($propsContent -notmatch "<Version>(?<version>\d+\.\d+\.\d+)</Version>") {
    throw "Version not found in Directory.Build.props"
}

$current = [version]$Matches["version"]
$newVersion = switch ($Bump) {
    "major" { [version]::new($current.Major + 1, 0, 0) }
    "minor" { [version]::new($current.Major, $current.Minor + 1, 0) }
    "patch" { [version]::new($current.Major, $current.Minor, $current.Build + 1) }
}

$versionText = $newVersion.ToString(3)
$manifestVersion = "$versionText.0"

Write-Host "Bumping version ($Bump): $($current.ToString(3)) -> $versionText"

$propsContent = $propsContent -replace "<Version>\d+\.\d+\.\d+</Version>", "<Version>$versionText</Version>"
$propsContent = $propsContent -replace "<InformationalVersion>\d+\.\d+\.\d+</InformationalVersion>", "<InformationalVersion>$versionText</InformationalVersion>"
Write-Utf8NoBom -Path $propsPath -Content $propsContent

$manifestContent = Get-Content -Path $manifestPath -Raw
$manifestContent = $manifestContent -replace 'assemblyIdentity version="\d+\.\d+\.\d+\.\d+"', "assemblyIdentity version=`"$manifestVersion`""
Write-Utf8NoBom -Path $manifestPath -Content $manifestContent

if (Test-Path $wxsPath) {
    $wxsContent = Get-Content -Path $wxsPath -Raw
    $wxsContent = $wxsContent -replace 'Version="\d+\.\d+\.\d+"', "Version=`"$versionText`""
    Write-Utf8NoBom -Path $wxsPath -Content $wxsContent
}

if ($env:GITHUB_OUTPUT) {
    "VERSION=$versionText" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

Write-Host "New version: $versionText"
