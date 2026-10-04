param (
    [string]$packageVersion= $(Read-Host "package version") 
)

$projects = @(
    "StdUnit.Tags.Core",
    "StdUnit.Tags", 
    "StdUnit.Tags.McpServer",

    "StdUnit.Tags.S7", 
    "StdUnit.Tags.SimpleFiles", 
    "StdUnit.Tags.ModbusTcp", 
    "StdUnit.Tags.ZLan",
    "StdUnit.Tags.Hjzk",
    "StdUnit.Tags.OpcUaClient",
    "StdUnit.Tags.ComScanner",

    "StdUnit.Tags.RxExtensions",
    "StdUnit.Tags.R3Extensions",

    "StdUnit.Tags.BlazorLib.Core", 
    "StdUnit.Tags.BlazorLib"
    )
$nugetSource = $Env:NugetSource
$key = $Env:NugetApiKey

if([String]::IsNullOrEmpty($nugetSource))
{
    throw "NugetSource is empty";
}

if([String]::IsNullOrEmpty($key))
{
    throw "NugetKey is empty";
}

$baseDir=Split-Path $script:MyInvocation.MyCommand.Path
Push-Location $baseDir

$projects | ForEach-Object -Process{
    $projName = $_
    Write-host "[+]****************start :$($projName)*************************"
    $distPath = Join-Path -Path $baseDir -ChildPath $projName
    Push-Location $distPath
    Write-Host "[-] current location: $distPath"

    dotnet pack -c Release -p:PackageVersion=$($packageVersion)
    if($LASTEXITCODE -ne 0)
    {
        throw
    }

    dotnet nuget push -s $($nugetSource) -k $($key) ./bin/Release/$($projName).$($packageVersion).nupkg
    if($LASTEXITCODE -ne 0)
    {
        throw
    }

    Pop-Location
    Write-host "[-]****************end :$($projName)*************************"
    Write-host ""
    Write-host ""
}