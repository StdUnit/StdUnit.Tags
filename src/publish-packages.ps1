param (
    [Parameter(Mandatory=$true)]
    [string]$packageVersion,
    [string]$nugetSource = $Env:NugetSource,
    [string]$nugetKey = $Env:NugetApiKey,
    # 只打包、不推送：首次发版前用它验证产物（14 个包能不能 pack 出来），或排障时不污染包源
    [switch]$SkipPush
)

# 版本号必须先校验：nuget.org 上的版本不可撤销，手误（漏一段、写成 1.0、覆盖已发版本）代价很大
if ($packageVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$')
{
    throw "packageVersion 必须是 <major>.<minor>.<patch>[-<prerelease>] 形式（如 1.0.0 或 1.0.0-rc.1），当前为：$packageVersion";
}

if (-not $SkipPush)
{
    if([String]::IsNullOrEmpty($nugetSource))
    {
        throw "NugetSource is empty";
    }

    if([String]::IsNullOrEmpty($nugetKey))
    {
        throw "NugetKey is empty";
    }
}

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

$baseDir=Split-Path $script:MyInvocation.MyCommand.Path
Push-Location $baseDir

$projects | ForEach-Object -Process{
    $projName = $_
    Write-host "[+]****************start :$($projName)*************************"

    $distPath = Join-Path -Path $baseDir -ChildPath $projName
    Push-Location $distPath
    try{
        Write-Host "[-] current location: $distPath"

        dotnet pack -c Release -p:PackageVersion=$($packageVersion)
        if($LASTEXITCODE -ne 0)
        {
            throw
        }

        if ($SkipPush)
        {
            Write-Host "[-] -SkipPush：已生成 $(Join-Path $distPath "bin/Release/$($projName).$($packageVersion).nupkg")，跳过推送"
        }
        else
        {
            # --skip-duplicate：发布中途失败后重跑时，已推上去的包不再让整条流水线失败
            dotnet nuget push -s "$($nugetSource)" -k "$($nugetKey)" --skip-duplicate ./bin/Release/$($projName).$($packageVersion).nupkg
            if($LASTEXITCODE -ne 0)
            {
                throw
            }
        }

    }
    finally{
        Pop-Location
        Write-host "[-]****************end :$($projName)*************************"
        Write-host ""
        Write-host ""
    }
}