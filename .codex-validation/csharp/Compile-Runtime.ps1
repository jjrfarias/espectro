$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $taskRoot '..\..\client'))
$unityData = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data'
$compilerHost = Join-Path $unityData 'DotNetSdk\dotnet.exe'
$compilerDll = Join-Path $unityData 'DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot 'dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$configurations = @(
    @{ Name = 'Editor'; Artifact = '2000b0aE.dag' },
    @{ Name = 'WebGL'; Artifact = '2000b0aPDev.dag' }
)
$results = @()
Push-Location $projectRoot
try {
    foreach ($configuration in $configurations) {
        $responseSource = Join-Path $projectRoot ('Library\Bee\artifacts\' + $configuration.Artifact + '\Assembly-CSharp.rsp')
        $configurationDir = Join-Path $taskRoot $configuration.Name
        [System.IO.Directory]::CreateDirectory($configurationDir) | Out-Null
        $outputDll = (Join-Path $configurationDir 'Assembly-CSharp.dll').Replace('\', '/')
        $referenceDll = (Join-Path $configurationDir 'Assembly-CSharp.ref.dll').Replace('\', '/')
        $responseLines = [System.Collections.Generic.List[string]]::new()
        foreach ($line in [System.IO.File]::ReadAllLines($responseSource)) {
            if ($line.StartsWith('-out:')) { $responseLines.Add('-out:"' + $outputDll + '"'); continue }
            if ($line.StartsWith('-refout:')) { $responseLines.Add('-refout:"' + $referenceDll + '"'); continue }
            if ($line -match '^"Assets/Scripts/.*\.cs"$') { continue }
            $responseLines.Add($line)
        }
        $sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets\Scripts') -Filter '*.cs' -Recurse | Sort-Object FullName
        foreach ($sourceFile in $sourceFiles) {
            $relativeSource = $sourceFile.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
            $responseLines.Add('"' + $relativeSource + '"')
        }
        $responseOutput = Join-Path $configurationDir 'Assembly-CSharp.rsp'
        [System.IO.File]::WriteAllLines($responseOutput, $responseLines, $utf8)
        $hashes = $sourceFiles | ForEach-Object {
            @{ Path = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        }
        [System.IO.File]::WriteAllText((Join-Path $configurationDir 'source-hashes.json'), ($hashes | ConvertTo-Json -Depth 3), $utf8)
        $compilerOutput = @(& $compilerHost $compilerDll ('@' + $responseOutput) 2>&1)
        $compilerExitCode = $LASTEXITCODE
        [System.IO.File]::WriteAllLines((Join-Path $configurationDir 'compile.log'), [string[]]$compilerOutput, $utf8)
        $results += @{ Configuration = $configuration.Name; ExitCode = $compilerExitCode; SourceCount = $sourceFiles.Count }
        Write-Output ($configuration.Name + ': exit ' + $compilerExitCode + ', ' + $sourceFiles.Count + ' runtime sources')
        $compilerOutput | Write-Output
    }
}
finally { Pop-Location }
[System.IO.File]::WriteAllText((Join-Path $taskRoot 'results.json'), ($results | ConvertTo-Json), $utf8)
if (($results | Where-Object { $_.ExitCode -ne 0 }).Count -gt 0) { exit 1 }
