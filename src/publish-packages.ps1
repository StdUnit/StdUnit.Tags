[CmdletBinding(DefaultParameterSetName = 'Explicit')]
param (
    # 正式发布的版本号（语义化版本）：release-*.yml 通过 workflow_dispatch 的输入显式传入。
    [Parameter(Mandatory=$true, ParameterSetName='Explicit')]
    [string]$packageVersion,

    # 迭代产物（dev 通道）：只给构建号（如 CI 的 "<run_number>.<run_attempt>"），
    # 基础版本从仓库根的 next-release-version.txt 读取，拼成 <base>-dev.<DevBuildNumber>。
    # "下一个计划发布的正式号"因此只有一处来源，不会在 workflow 里硬编码。
    [Parameter(Mandatory=$true, ParameterSetName='Dev')]
    [string]$DevBuildNumber,

    [string]$nugetSource = $Env:NugetSource,
    [string]$nugetKey = $Env:NugetApiKey,
    # 只打包、不推送：首次发版前用它验证产物（13 个包能不能 pack 出来），或排障时不污染包源；
    # CI 的 dev-packages 通道也用它——只产出"迭代产物"（配 -DevBuildNumber 传构建号），不推任何源。
    [switch]$SkipPush,
    # 产物输出目录（为空则沿用 dotnet pack 默认：各项目自己的 bin/Release）。
    # 相对路径按本脚本所在目录（src/）解析。指定它可以让产物集中到一处，避免 bin/Release 里
    # 的历史 nupkg 混进来（发布通道与推送路径都跟着用它）。
    [string]$OutputDir
)

$versionPattern = '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$'

if ($PSCmdlet.ParameterSetName -eq 'Dev')
{
    # 版本文件的唯一来源就是仓库根的这个文件（本脚本位于 src/ 下），改号只改它
    $versionFile = Join-Path -Path (Split-Path -Parent $PSScriptRoot) -ChildPath 'next-release-version.txt'
    if (-not (Test-Path -LiteralPath $versionFile))
    {
        throw "找不到版本文件：$versionFile（dev 通道需要它提供基础版本号）";
    }

    $baseVersion = "$(Get-Content -LiteralPath $versionFile -Raw)".Trim()
    if ($baseVersion -notmatch $versionPattern)
    {
        throw "next-release-version.txt 的内容必须是 <major>.<minor>.<patch>[-<prerelease>] 形式（如 2.0.0），当前为：'$baseVersion'";
    }

    $packageVersion = "$baseVersion-dev.$DevBuildNumber"
}

# 版本号必须先校验：nuget.org 上的版本不可撤销，手误（漏一段、写成 1.0、覆盖已发版本）代价很大
if ($packageVersion -notmatch $versionPattern)
{
    $origin = if ($PSCmdlet.ParameterSetName -eq 'Dev') { "next-release-version.txt 与 -DevBuildNumber 拼出的版本" } else { "-packageVersion" }
    throw "$origin 必须是 <major>.<minor>.<patch>[-<prerelease>] 形式（如 1.0.0 或 1.0.0-rc.1），当前为：$packageVersion";
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
    "StdUnit.Tags.WebUi"
    )

$baseDir=Split-Path $script:MyInvocation.MyCommand.Path
Push-Location $baseDir

# 归一到绝对路径：后面每个项目都会 Push-Location 到自己的目录，相对路径在那里就不再有效
if (-not [String]::IsNullOrEmpty($OutputDir))
{
    $OutputDir = if ([IO.Path]::IsPathRooted($OutputDir))
    {
        [IO.Path]::GetFullPath($OutputDir)
    }
    else
    {
        [IO.Path]::GetFullPath((Join-Path $baseDir $OutputDir))
    }
}

$projects | ForEach-Object -Process{
    $projName = $_
    Write-host "[+]****************start :$($projName)*************************"

    $distPath = Join-Path -Path $baseDir -ChildPath $projName
    # 未指定 -OutputDir 时，dotnet pack 默认就落在项目自己的 bin/Release
    $packOutputDir = if ([String]::IsNullOrEmpty($OutputDir)) { Join-Path $distPath "bin/Release" } else { $OutputDir }

    Push-Location $distPath
    try{
        Write-Host "[-] current location: $distPath"

        # 用数组 splat，避免 -OutputDir 带空格时被拆成多个参数
        $packArgs = @("-c", "Release", "-p:PackageVersion=$($packageVersion)")
        if (-not [String]::IsNullOrEmpty($OutputDir))
        {
            $packArgs += @("-o", $OutputDir)
        }
        dotnet pack @packArgs
        if($LASTEXITCODE -ne 0)
        {
            throw
        }

        $nupkgPath = Join-Path $packOutputDir "$($projName).$($packageVersion).nupkg"

        if ($SkipPush)
        {
            Write-Host "[-] -SkipPush：已生成 $nupkgPath，跳过推送"
        }
        else
        {
            # --skip-duplicate：发布中途失败后重跑时，已推上去的包不再让整条流水线失败
            dotnet nuget push -s "$($nugetSource)" -k "$($nugetKey)" --skip-duplicate $nupkgPath
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