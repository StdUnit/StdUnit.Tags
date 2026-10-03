#Requires -Version 7.0
<#
.SYNOPSIS
    检测 Paket 跨 group 的「同名包不同版本」冲突。

.DESCRIPTION
    背景：Paket 的 group 之间完全独立解析，不做任何一致性检查。若同一个项目（同一 TFM）
    同时引用了多个 group，而两个 group 对同一个包解析出了**不同版本**，就会产生：
      * Paket 硬报错   "Package X is referenced in different versions in <proj> (A vs B)"
        → `paket install` 退出码 1
      * NuGet 告警     NU1504 / NU1506（重复 PackageReference / PackageVersion）
      * MSBuild 告警   MSB3277（"Found conflicts between different versions of X"）
    后两者不阻断构建，但 NuGet 明说此时行为不可靠（版本选择未定义，实测 net472 下可能取到低版本）。

    不变式（本脚本检查的就是它）：
      对任意包 P、项目 π、TFM t：若 P 同时来自 π 引用的多个 group，则这些 group 里 P 的版本必须相同。

    实现：`obj/<proj>.<tfm>.paket.resolved` 是 Paket 写出的 CSV
    （Name,Version,Kind,Group,...），直接聚合即可。

    注意：
      * 必须扫**全仓库**（含 samples/），不能只扫 src/ —— 冲突是按 (项目, TFM) 计算的。
      * paket.resolved 是按项目自己的 paket.references 算闭包，**不看 ProjectReference**，
        所以「项目 A 引用项目 B」不会让 A 继承 B 的 direct 包。

.PARAMETER Root
    仓库根目录。默认取脚本所在目录。

.PARAMETER Detail
    额外打印每个冲突包的完整版本/来源 group 明细。

.EXAMPLE
    pwsh ./check-paket-conflicts.ps1

.EXAMPLE
    pwsh ./check-paket-conflicts.ps1 -Detail

.NOTES
    退出码：0 = 无冲突；1 = 存在冲突；2 = 找不到任何 paket.resolved（通常表示先跑 `dotnet paket restore`）。
#>
[CmdletBinding()]
param(
    [string]$Root = $PSScriptRoot,
    [switch]$Detail
)

$ErrorActionPreference = 'Stop'

$resolvedFiles = @(Get-ChildItem -Path $Root -Recurse -Filter '*.paket.resolved' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(node_modules|\.git)\\' })

if ($resolvedFiles.Count -eq 0) {
    Write-Host "未找到任何 *.paket.resolved —— 请先运行 'dotnet paket restore'。" -ForegroundColor Yellow
    exit 2
}

$conflictCount = 0
$reportedFiles = 0

foreach ($file in $resolvedFiles) {
    # paket.resolved 是 CSV：Name,Version,Kind,Group,...
    $entries = foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $f = $line -split ','
        if ($f.Count -ge 4 -and -not [string]::IsNullOrWhiteSpace($f[0])) {
            [pscustomobject]@{
                Name    = $f[0]
                Version = $f[1]
                Kind    = $f[2]
                Group   = $f[3]
            }
        }
    }

    $dups = $entries |
        Group-Object Name |
        Where-Object { ($_.Group.Version | Sort-Object -Unique).Count -gt 1 }

    if (-not $dups) { continue }

    $reportedFiles++
    $relative = [System.IO.Path]::GetRelativePath($Root, $file.FullName)
    Write-Host "[$relative]" -ForegroundColor Red

    foreach ($dup in $dups) {
        $conflictCount++
        $breakdown = ($dup.Group | Sort-Object Version | ForEach-Object { "$($_.Version) [$($_.Group)]" }) -join '  vs  '
        Write-Host "    $($dup.Name): $breakdown" -ForegroundColor Yellow

        if ($Detail) {
            foreach ($g in ($dup.Group | Sort-Object Version)) {
                Write-Host "        - $($g.Version)  group=$($g.Group)  kind=$($g.Kind)" -ForegroundColor DarkGray
            }
        }
    }
}

Write-Host ''

if ($conflictCount -gt 0) {
    Write-Host "发现 $conflictCount 处跨 group 版本冲突（涉及 $reportedFiles 个项目/TFM）。" -ForegroundColor Red
    Write-Host "修法：让涉及的 group 对同一个包取**相同版本**（通常是把低的一方提到高的一方）。" -ForegroundColor Yellow
    Write-Host "注意 Paket 无跨组约束机制，只能靠 paket.dependencies 里的约束钉；本脚本就是用来验证钉得全不全。" -ForegroundColor Yellow
    exit 1
}

Write-Host "未发现跨 group 版本冲突（已检查 $($resolvedFiles.Count) 个 paket.resolved）。" -ForegroundColor Green
exit 0
