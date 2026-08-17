[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $repoRoot 'src\VALE.Client\VALE.Client.csproj') -p:Platform=x64

