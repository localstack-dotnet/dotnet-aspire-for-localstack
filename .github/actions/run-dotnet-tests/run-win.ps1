Param(
  [string]$ProjectPath,
  [string]$ResultsDir,
  [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

# Pull both TargetFramework *and* TargetFrameworks so we handle single-TFM too.
$tfmRaw = dotnet msbuild $ProjectPath `
          -getProperty:TargetFrameworks,TargetFramework -nologo -v:q
if (-not $tfmRaw) { throw "Unable to determine target frameworks for $ProjectPath" }

$tfms = ($tfmRaw -replace '[\r\n]+',';').Split(';') |
        Where-Object { $_ } | Select-Object -Unique

Write-Host "📋 Target frameworks: $($tfms -join ', ')"

foreach ($tfm in $tfms) {
    Write-Host "🧪 $tfm ..."
    dotnet test $ProjectPath -c $Configuration -f $tfm --no-build `
        --logger "trx;LogFileName=testResults-$tfm.trx" `
        --results-directory $ResultsDir
}
