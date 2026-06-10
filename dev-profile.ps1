# CreaturePrefabCreator — Dev Profile Sync Script
# Copies gameplay/utility mods from client to server dev environment,
# EXCLUDING graphics/texture/visual improvement mods.
#
# Usage:
#   .\dev-profile.ps1                    # copy to default V:\server location
#   .\dev-profile.ps1 -Target "C:\Dev"   # copy to custom path
#   .\dev-profile.ps1 -WhatIf            # preview what would be copied
#   .\dev-profile.ps1 -NoBuild           # skip CPC DLL auto-copy
#   .\dev-profile.ps1 -NoLog             # disable log file output
#   .\dev-profile.ps1 -CleanStale        # move stale target files to quarantine

param(
    [string]$Source = "D:\Games and Apps\SteamLibrary\steamapps\common\Valheim\BepInEx\plugins",
    [string]$Target = "V:\server\BepInEx\plugins",
    [string]$CpcBuildDir = "C:\Users\16265\DevelopmentProjects\ValheimModding\CreaturePrefabCreator\bin\Debug",
    [switch]$NoBuild,
    [switch]$NoLog,
    [switch]$CleanStale,
    [switch]$WhatIf
)

# ─── Log helpers ─────────────────────────────────────────────────────────────
$_log = [System.Collections.Generic.List[string]]::new()
$_startTime = Get-Date

function Log([string]$msg) {
    # Strip any ANSI escape codes before storing
    $plain = $msg -replace '\x1B\[[0-9;]*m', ''
    $_log.Add($plain)
}

# ─── Graphics / HD Texture / Visual improvement mods to EXCLUDE ──────────────
$ExcludePatterns = @(
    # Texture tools & scripts
    "AnalyzeTextures.ps1"
    "CreateLowResTextures.ps1"
    "RestoreOriginalTextures.ps1"
    "ScaleTo512.ps1"
    "MoveBackupsToStorage.ps1"
    # Visual particle / dust removal
    "NoBuildDust.dll"
    "NoCultivatorDust.dll"
    "NoHoeDust.dll"
    "NoWeaponDust.dll"
    # Rendering / camera / first-person visual mods
    "RenderLimits.dll"
    "ImmersiveFirstPerson.dll"
    "MinimalStatusEffects.dll"
    # Debug / inspector tools (not for runtime)
    "UnityExplorer.BIE5.Mono.dll"
    "UniverseLib.Mono.dll"
    "dnSpy-net-win64"
)

# Core dependencies that must always be present
$Required = @(
    "Jotunn.dll"
    "Newtonsoft.Json.dll"
    "NewtonsoftJsonDetector.dll"
)

function Should-Include($name) {
    foreach ($pat in $ExcludePatterns) {
        if ($name -like "*$pat*") { return $false }
    }
    return $true
}

# Tracks which relative paths were included (for stale detection)
$_includedRels = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)

$_copiedCount = 0
$_skippedCount = 0

function Copy-DevItem($srcPath, $dstPath) {
    $item = Get-Item $srcPath
    $rel = $srcPath.Substring($Source.Length).TrimStart('\','/')
    [void]$_includedRels.Add($rel)

    if ($item -is [System.IO.DirectoryInfo]) {
        if (-not $WhatIf) {
            if (-not (Test-Path $dstPath)) { New-Item -ItemType Directory -Path $dstPath -Force | Out-Null }
        }
        $msg = "DIR  $rel"
        Write-Host $msg -ForegroundColor Cyan
        Log $msg
    } else {
        $srcHash = (Get-FileHash $srcPath -Algorithm SHA256).Hash
        $dstHash = if (Test-Path $dstPath) { (Get-FileHash $dstPath -Algorithm SHA256).Hash } else { $null }

        if ($srcHash -ne $dstHash) {
            if ($WhatIf) {
                $msg = "COPY $rel  (would update)"
                Write-Host $msg -ForegroundColor Yellow
            } else {
                Copy-Item $srcPath $dstPath -Force
                $msg = "COPY $rel"
                Write-Host $msg -ForegroundColor Green
                $script:_copiedCount++
            }
            Log $msg
        } else {
            $msg = "SKIP $rel  (identical)"
            Write-Host $msg -ForegroundColor DarkGray
            Log $msg
            $script:_skippedCount++
        }
    }
}

# ─── Validate paths ──────────────────────────────────────────────────────────
if (-not (Test-Path $Source)) {
    $err = "ERROR: Source not found: $Source"
    Write-Error $err
    Log $err
    exit 1
}

if (-not $WhatIf -and -not (Test-Path $Target)) {
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
    $msg = "Created target directory: $Target"
    Write-Host $msg -ForegroundColor Green
    Log $msg
}

$hdr = @(
    "Script start : $($_startTime.ToString('yyyy-MM-dd HH:mm:ss'))"
    "Source       : $Source"
    "Target       : $Target"
)
$hdr | ForEach-Object { Log $_ }

Write-Host "`nSource: $Source" -ForegroundColor White
Write-Host "Target: $Target" -ForegroundColor White
if ($WhatIf) { Write-Host "MODE: WhatIf (no changes will be made)`n" -ForegroundColor Magenta }
else { Write-Host "" }

# ─── Gather and filter items ─────────────────────────────────────────────────
$allItems = Get-ChildItem -Path $Source -Depth 2
$included = @()
$excluded = @()

foreach ($item in $allItems) {
    if (Should-Include $item.Name) {
        $included += $item
    } else {
        $excluded += $item
    }
}

# ─── Report excluded ─────────────────────────────────────────────────────────
Write-Host "Excluded (graphics/visual/texture/debug):" -ForegroundColor Red
Log "Excluded (graphics/visual/texture/debug):"
$excluded | Select-Object -ExpandProperty Name | Sort-Object | ForEach-Object {
    Write-Host "  [-] $_" -ForegroundColor DarkRed
    Log "  [-] $_"
}
Write-Host ""

# ─── Copy included ───────────────────────────────────────────────────────────
Write-Host "Included (gameplay/utility/dependencies):" -ForegroundColor Green
Log "Included (gameplay/utility/dependencies):"
$included | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($Source.Length).TrimStart('\','/')
    $dst = Join-Path $Target $rel
    Copy-DevItem $_.FullName $dst
}

# ─── CPC DLL auto-copy ───────────────────────────────────────────────────────
Write-Host ""
$_cpcResult = ""
if (-not $NoBuild) {
    if (-not (Test-Path $CpcBuildDir)) {
        $_cpcResult = "CPC DLL  : WARN — build dir not found: $CpcBuildDir"
        Write-Warning "CPC build dir not found: $CpcBuildDir"
    } else {
        $cpcDlls = Get-ChildItem -Path $CpcBuildDir -Recurse -Filter "CreaturePrefabCreator.dll" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending
        if ($cpcDlls.Count -eq 0) {
            $_cpcResult = "CPC DLL  : WARN — CreaturePrefabCreator.dll not found under $CpcBuildDir"
            Write-Warning "CreaturePrefabCreator.dll not found under $CpcBuildDir"
        } else {
            $cpcSrc = $cpcDlls[0].FullName
            $cpcDst = Join-Path $Target "CreaturePrefabCreator.dll"
            $srcHash = (Get-FileHash $cpcSrc -Algorithm SHA256).Hash
            $dstHash = if (Test-Path $cpcDst) { (Get-FileHash $cpcDst -Algorithm SHA256).Hash } else { $null }

            if ($srcHash -ne $dstHash) {
                if ($WhatIf) {
                    $_cpcResult = "CPC DLL  : COPY CreaturePrefabCreator.dll (would update)"
                    Write-Host $_cpcResult -ForegroundColor Yellow
                } else {
                    Copy-Item $cpcSrc $cpcDst -Force
                    $_cpcResult = "CPC DLL  : COPY CreaturePrefabCreator.dll (updated from $cpcSrc)"
                    Write-Host "CPC DLL  : COPY CreaturePrefabCreator.dll (updated)" -ForegroundColor Green
                }
            } else {
                $_cpcResult = "CPC DLL  : SKIP CreaturePrefabCreator.dll (identical)"
                Write-Host $_cpcResult -ForegroundColor DarkGray
            }
        }
    }
} else {
    $_cpcResult = "CPC DLL  : skipped (-NoBuild)"
    Write-Host $_cpcResult -ForegroundColor DarkGray
}
Log $_cpcResult

# ─── Stale file detection / optional quarantine ───────────────────────────────
$_staleFiles = @()
if (Test-Path $Target) {
    $targetItems = Get-ChildItem -Path $Target -Depth 2 -File -ErrorAction SilentlyContinue
    foreach ($tf in $targetItems) {
        $rel = $tf.FullName.Substring($Target.Length).TrimStart('\','/')
        # Skip the CPC DLL and quarantine folder from stale checks
        if ($rel -like "_quarantine*") { continue }
        if (-not $_includedRels.Contains($rel) -and $tf.Name -ne "CreaturePrefabCreator.dll") {
            $_staleFiles += $tf
        }
    }
}

$_staleResult = ""
if ($_staleFiles.Count -gt 0) {
    Write-Host "`nStale files in Target (not in Source):" -ForegroundColor Yellow
    Log "`nStale files in Target (not in Source):"
    $_staleFiles | Sort-Object FullName | ForEach-Object {
        $rel = $_.FullName.Substring($Target.Length).TrimStart('\','/')
        Write-Host "  [STALE] $rel" -ForegroundColor DarkYellow
        Log "  [STALE] $rel"
    }

    if ($CleanStale) {
        $quarantineStamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
        $quarantineDir = Join-Path $Target "_quarantine\$quarantineStamp"
        if ($WhatIf) {
            $_staleResult = "Stale    : $($_staleFiles.Count) file(s) — would move to $quarantineDir"
            Write-Host "`n$_staleResult" -ForegroundColor Magenta
        } else {
            New-Item -ItemType Directory -Path $quarantineDir -Force | Out-Null
            foreach ($sf in $_staleFiles) {
                $dst = Join-Path $quarantineDir $sf.Name
                Move-Item $sf.FullName $dst -Force
                $logLine = "  QUARANTINE: $($sf.Name) -> $dst"
                Write-Host $logLine -ForegroundColor DarkYellow
                Log $logLine
            }
            $_staleResult = "Stale    : $($_staleFiles.Count) file(s) moved to quarantine: $quarantineDir"
        }
    } else {
        $_staleResult = "Stale    : $($_staleFiles.Count) file(s) in Target not in Source  [use -CleanStale to quarantine]"
        Write-Host "`n$_staleResult" -ForegroundColor Yellow
    }
} else {
    $_staleResult = "Stale    : none"
    Write-Host "`nStale    : none" -ForegroundColor DarkGray
}
Log $_staleResult

# ─── Required verification ───────────────────────────────────────────────────
$_missingRequired = @()
foreach ($req in $Required) {
    if (-not (Test-Path (Join-Path $Target $req))) {
        $_missingRequired += $req
    }
}

$_requiredResult = ""
if ($_missingRequired.Count -eq 0) {
    $_requiredResult = "Required : OK ($($Required.Count)/$($Required.Count))"
    Write-Host "`n$_requiredResult" -ForegroundColor Green
} else {
    $_requiredResult = "Required : MISSING $($_missingRequired.Count)/$($Required.Count)"
    Write-Host "`n$_requiredResult" -ForegroundColor Red
    $_missingRequired | ForEach-Object {
        $w = "  [MISSING] $_"
        Write-Warning $w
        Log $w
    }
}
Log $_requiredResult

# ─── Summary ─────────────────────────────────────────────────────────────────
$_endTime = Get-Date
$_duration = [math]::Round(($_endTime - $_startTime).TotalSeconds, 1)

Write-Host "`n────────────────────────────────────────" -ForegroundColor White
Write-Host "Excluded : $($excluded.Count) items" -ForegroundColor Red
Write-Host "Included : $($included.Count) items" -ForegroundColor Green
if (-not $WhatIf) {
    Write-Host "Copied   : $_copiedCount" -ForegroundColor Green
    Write-Host "Skipped  : $_skippedCount" -ForegroundColor DarkGray
}
Write-Host "$_cpcResult" -ForegroundColor $(if ($_cpcResult -like "*WARN*") { "Yellow" } elseif ($_cpcResult -like "*COPY*") { "Green" } else { "DarkGray" })
Write-Host "$_staleResult" -ForegroundColor $(if ($_staleFiles.Count -gt 0) { "Yellow" } else { "DarkGray" })
Write-Host "$_requiredResult" -ForegroundColor $(if ($_missingRequired.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Target   : $Target" -ForegroundColor White
Write-Host "Duration : ${_duration}s" -ForegroundColor DarkGray

Log ""
Log "────────────────────────────────────────"
Log "Excluded : $($excluded.Count) items"
Log "Included : $($included.Count) items"
if (-not $WhatIf) {
    Log "Copied   : $_copiedCount"
    Log "Skipped  : $_skippedCount"
}
Log $_cpcResult
Log $_staleResult
Log $_requiredResult
Log "Target   : $Target"
Log "Script end   : $($_endTime.ToString('yyyy-MM-dd HH:mm:ss'))  (${_duration}s)"

if ($WhatIf) {
    Write-Host "`nRun without -WhatIf to perform actual copy." -ForegroundColor Magenta
}

# ─── Write log file ──────────────────────────────────────────────────────────
if (-not $NoLog -and -not $WhatIf) {
    $logDir = Join-Path $PSScriptRoot "Logs"
    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }
    $logFile = Join-Path $logDir ("dev-profile-" + $_startTime.ToString("yyyy-MM-dd_HH-mm-ss") + ".log")
    $_log | Out-File -FilePath $logFile -Encoding UTF8
    Write-Host "Log      : $logFile" -ForegroundColor DarkCyan
}

# ─── Exit code ───────────────────────────────────────────────────────────────
if (-not $WhatIf -and $_missingRequired.Count -gt 0) {
    exit 1
}
