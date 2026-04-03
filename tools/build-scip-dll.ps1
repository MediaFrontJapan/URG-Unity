$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src/MediaFrontJapan.SCIP/MediaFrontJapan.SCIP.csproj"
$configuration = "Release"
$framework = "netstandard2.1"
$outputDir = Join-Path $repoRoot "src/MediaFrontJapan.SCIP/bin/$configuration/$framework"
$pluginDir = Join-Path $repoRoot "Packages/com.mediafrontjapan.urg-unity/Runtime/Plugins"
$dllName = "MediaFrontJapan.SCIP.dll"

dotnet build $projectPath -c $configuration -nologo

Copy-Item -LiteralPath (Join-Path $outputDir $dllName) -Destination (Join-Path $pluginDir $dllName) -Force

Get-ChildItem -LiteralPath $pluginDir -Filter "*.SCIP.dll" -File |
    Where-Object { $_.Name -ne $dllName } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

Get-ChildItem -LiteralPath $pluginDir -Filter "*.SCIP.pdb" -File |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
