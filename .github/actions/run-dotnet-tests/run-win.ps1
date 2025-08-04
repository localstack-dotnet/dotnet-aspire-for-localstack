Param(
  [string]$ProjectPath,
  [string]$ResultsDir,
  [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

# 1️⃣  Multi-target first
$tfmRaw = dotnet msbuild $ProjectPath `
          -getProperty:TargetFrameworks -nologo -v:q

# 2️⃣  Fallback to single-target
if ([string]::IsNullOrWhiteSpace($tfmRaw)) {
  $tfmRaw = dotnet msbuild $ProjectPath `
            -getProperty:TargetFramework -nologo -v:q
}

if ([string]::IsNullOrWhiteSpace($tfmRaw)) {
  throw "Unable to determine target frameworks for $ProjectPath"
}

$tfms = $tfmRaw -split ';' |
        ForEach-Object { $_.Trim() } |
        Where-Object  { $_ } |
        Select-Object -Unique

Write-Host "📋 Target frameworks: $($tfms -join ', ')"

foreach ($tfm in $tfms) {
    Write-Host "🧪 $tfm ..."
    dotnet test $ProjectPath -c $Configuration -f $tfm --no-build `
        --logger "trx;LogFileName=testResults-$tfm.trx" `
        --results-directory $ResultsDir
}
