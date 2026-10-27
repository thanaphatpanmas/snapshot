param([switch]$Test)

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$references = @(
    'System.Windows.Forms.dll',
    'System.Drawing.dll',
    'Microsoft.CSharp.dll',
    'Microsoft.VisualBasic.dll',
    (Join-Path $framework 'System.Runtime.WindowsRuntime.dll')
) | ForEach-Object { "/reference:$_" }

$sources = @(Get-ChildItem "$PSScriptRoot\src" -Filter *.cs -Recurse)
if ($Test) {
    # The test harness has its own Main, so the app's entry point is left out.
    $sources = @($sources | Where-Object { $_.Name -ne 'Program.cs' }) + (Get-Item "$PSScriptRoot\tests\SelfTests.cs")
    $target = 'exe'
    $output = "$PSScriptRoot\tests\SelfTests.exe"
} else {
    $target = 'winexe'
    $output = "$PSScriptRoot\Snapshot.exe"
}

& $compiler /nologo /codepage:65001 /target:$target /out:$output @references ($sources | ForEach-Object { $_.FullName })
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Test) {
    # The tests read src\ and write tests\out\ relative to the repository root.
    Push-Location $PSScriptRoot
    & $output
    $testExitCode = $LASTEXITCODE
    Pop-Location
    exit $testExitCode
}
