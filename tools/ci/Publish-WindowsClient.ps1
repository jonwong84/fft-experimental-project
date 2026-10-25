# Windows PowerShell 5.1 compatible. Never deletes or reuses an output directory.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $OutputRoot,
    [string] $DotNet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne 'Win32NT' -or -not [Environment]::Is64BitProcess) {
    throw 'Run this check with x64 PowerShell on Windows.'
}
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scratchRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'scratch')) + [IO.Path]::DirectorySeparatorChar
$output = if ([IO.Path]::IsPathRooted($OutputRoot)) { [IO.Path]::GetFullPath($OutputRoot) }
    else { [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot)) }
if (-not $output.StartsWith($scratchRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputRoot must be a new directory inside this checkout''s ignored scratch directory.'
}
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }
$dotnetPath = (Get-Command $DotNet -ErrorAction Stop).Source
$expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
Push-Location $repoRoot
try {
    $actualSdk = & $dotnetPath --version
    if ($LASTEXITCODE -ne 0 -or $actualSdk -ne $expectedSdk) { throw "Expected SDK $expectedSdk; got $actualSdk" }
    $logs = New-Item -ItemType Directory -Path (Join-Path $output 'logs')
    function Invoke-DotNet([string] $Name, [string[]] $Arguments) {
        & $dotnetPath @Arguments | Tee-Object -FilePath (Join-Path $logs.FullName "$Name.log")
        if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
    }
    $client = 'src/Fft.Client/Fft.Client.csproj'
    Invoke-DotNet 'restore' @('restore', $client, '--runtime', 'win-x64', '-p:SelfContained=true', '-p:FftWindowsBundle=true', '--locked-mode')
    $bundle = Join-Path $output 'bundle'
    Invoke-DotNet 'publish' @('publish', $client, '-c', 'Release', '--runtime', 'win-x64', '--self-contained', 'true',
        '-p:FftWindowsBundle=true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:PublishAot=false',
        '--no-restore', '--output', $bundle, "-bl:$(Join-Path $logs.FullName 'publish.binlog')")

    # .NET's own notices are taken from the exact runtime pack used by this publish.
    $deps = Get-Content -LiteralPath (Join-Path $bundle 'Fft.Client.deps.json') -Raw | ConvertFrom-Json
    $runtimePack = @($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'runtimepack.Microsoft.NETCore.App.Runtime.win-x64/*' })
    if ($runtimePack.Count -ne 1) { throw 'Expected exactly one Windows .NET runtime pack in the published dependency manifest.' }
    $runtimeVersion = $runtimePack[0].Split('/')[-1]
    $assets = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Fft.Client/obj/project.assets.json') -Raw | ConvertFrom-Json
    $packageRoot = @($assets.packageFolders.PSObject.Properties.Name | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ "microsoft.netcore.app.runtime.win-x64/$runtimeVersion/LICENSE.TXT")
    }) | Select-Object -First 1
    if (-not $packageRoot) { throw 'Cannot locate notices for the published .NET runtime pack.' }
    Copy-Item -LiteralPath (Join-Path $packageRoot "microsoft.netcore.app.runtime.win-x64/$runtimeVersion/LICENSE.TXT") -Destination (Join-Path $bundle 'THIRD-PARTY/DotNet-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $packageRoot "microsoft.netcore.app.runtime.win-x64/$runtimeVersion/THIRD-PARTY-NOTICES.TXT") -Destination (Join-Path $bundle 'THIRD-PARTY/DotNet-THIRD-PARTY-NOTICES.txt')

    $required = @('Fft.Client.exe', 'Fft.Client.dll', 'Zodiac.Framework.dll', 'Zodiac.Content.dll', 'Tessera.Engine.dll',
        'MonoGame.Framework.dll', 'NVorbis.dll', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
        'SDL2.dll', 'openal.dll', 'WINDOWS-LAUNCH.md', 'THIRD-PARTY/README.md',
        'THIRD-PARTY/MonoGame-LICENSE.txt', 'THIRD-PARTY/SDL-LICENSE.txt', 'THIRD-PARTY/NVorbis-LICENSE.txt',
        'THIRD-PARTY/OpenAL-Soft-COPYING.txt', 'THIRD-PARTY/OpenAL-Soft-BSD-LICENSE.txt',
        'THIRD-PARTY/DotNet-LICENSE.txt', 'THIRD-PARTY/DotNet-THIRD-PARTY-NOTICES.txt')
    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $bundle $relative) -PathType Leaf)) { throw "Missing bundle file: $relative" }
    }
    # Positive inventory: build output and explicit notices only; nothing from scratch is a publish input.
    $inventory = @(Get-ChildItem -LiteralPath $bundle -File -Recurse | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ path = $_.FullName.Substring($bundle.Length + 1).Replace([IO.Path]::DirectorySeparatorChar, [char]'/');
            bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'bundle-inventory.json') -Encoding UTF8

    $launchCwd = New-Item -ItemType Directory -Path (Join-Path $output 'launch-cwd')
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $bundle 'Fft.Client.exe'
    $start.Arguments = '--scenario two-unit-battle'
    $start.WorkingDirectory = $launchCwd.FullName
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    # Ensure this process uses its bundled runtime, rather than a shared SDK/runtime location.
    $start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $launchCwd.FullName 'no-shared-runtime'
    $start.EnvironmentVariables['DOTNET_ROOT_X64'] = $start.EnvironmentVariables['DOTNET_ROOT']
    $start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'Could not start the published client.' }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            throw 'Published client exceeded the 30-second headless launch limit.'
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $stdout | Set-Content -LiteralPath (Join-Path $logs.FullName 'scenario.stdout.txt') -Encoding UTF8
        $stderr | Set-Content -LiteralPath (Join-Path $logs.FullName 'scenario.stderr.txt') -Encoding UTF8
        if ($process.ExitCode -ne 0) { throw "Published client failed: $($process.ExitCode); $stderr" }
        if ($stdout -notmatch 'HP: sample:unit/first=25, sample:unit/second=0; ticks=20; RNG=2473755763/22\.' -or
            $stdout -notmatch 'Outcome: FirstUnitWon; winner: sample:unit/first\.' -or
            ([regex]::Matches($stdout, '=> accepted')).Count -ne 12 -or $stdout -match '=> rejected') {
            throw 'Published client did not produce the expected synthetic battle result.'
        }
        [ordered]@{
            sdk = $actualSdk; bundledRuntime = $runtimeVersion; os = [Environment]::OSVersion.VersionString
            architecture = 'x64'; exitCode = $process.ExitCode; acceptedCommands = 12
            finalHp = @(25, 0); ticks = 20; rngWord = '2473755763'; rngDraws = 22
            emptyWorkingDirectory = $launchCwd.FullName; originalAssetsRequired = $false
            graphicalLaunch = 'not tested'; cleanMachine = 'not established by this developer/CI check'
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'launch-check.json') -Encoding UTF8
    } finally { $process.Dispose() }
    Write-Host "Published folder and headless check passed: $output"
} finally { Pop-Location }
