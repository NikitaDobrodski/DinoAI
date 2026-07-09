param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DinoArgs
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$tmpRoot = Join-Path $repoRoot ".tmp"
$dotnetHome = Join-Path $repoRoot ".dotnet"

New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null
New-Item -ItemType Directory -Force -Path $dotnetHome | Out-Null

$env:TEMP = $tmpRoot
$env:TMP = $tmpRoot
$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

$cliProject = Join-Path $repoRoot "src/DinoAI.Cli"
$cliDll = Join-Path $cliProject "bin/Debug/net10.0/DinoAI.Cli.dll"
$programFile = Join-Path $cliProject "Program.cs"

$needsBuild = !(Test-Path -LiteralPath $cliDll)
if (!$needsBuild -and (Test-Path -LiteralPath $programFile)) {
    $needsBuild = (Get-Item -LiteralPath $programFile).LastWriteTimeUtc -gt (Get-Item -LiteralPath $cliDll).LastWriteTimeUtc
}

if ($needsBuild) {
    $buildOutput = dotnet build $cliProject --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0 -and !(Test-Path -LiteralPath $cliDll)) {
        $buildOutput
        exit $LASTEXITCODE
    }
}

if (Test-Path -LiteralPath $cliDll) {
    dotnet $cliDll @DinoArgs
} else {
    dotnet run --project $cliProject -- @DinoArgs
}
