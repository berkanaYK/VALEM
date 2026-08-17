[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $repoRoot 'src\VALE.Api\VALE.Api.csproj') --launch-profile https

