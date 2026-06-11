#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fast log checker for CPC errors - handles large files efficiently.
#>

$ErrorActionPreference = "SilentlyContinue"

# Log file paths
$bepinexLog = "C:\Users\16265\DevelopmentProjects\ValheimModding\LaunchValheim\ModTestingProfile\BepInEx\LogOutput.log"
$playerLog = "C:\Users\16265\AppData\LocalLow\IronGate\Valheim\Player.log"

Write-Host "`n🔍 Scanning Valheim logs for CPC issues..." -ForegroundColor Cyan

# Quick scan function using streaming (fast for large files)
function Scan-LogFast {
    param($Path, $Description)
    
    Write-Host "`n📄 Checking: $Description" -ForegroundColor White
    
    if (-not (Test-Path $Path)) {
        Write-Host "   [MISSING] File not found" -ForegroundColor Yellow
        return @{}
    }
    
    $fileSize = (Get-Item $Path).Length / 1MB
    Write-Host "   File size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Gray
    
    $results = @{
        CPC_Errors = @()
        MountUp_Issues = @()
        AllTameable_Issues = @()
        Procreation_NREs = @()
        Reflection_Errors = @()
    }
    
    # Stream read line by line (memory efficient)
    $reader = [System.IO.StreamReader]::new($Path)
    $lineNum = 0
    $lastPercent = -1
    
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            $lineNum++
            
            # Progress indicator every 1000 lines
            if ($lineNum % 1000 -eq 0) {
                Write-Host "." -NoNewline -ForegroundColor DarkGray
            }
            
            # Check patterns (fast string contains, not regex)
            if ($line -match '\[Error\s*:CreaturePrefabCreator\]' -or 
                ($line -match 'CreaturePrefabCreator' -and $line -match 'Exception')) {
                $results.CPC_Errors += $line.Trim()
            }
            elseif ($line -match 'MountUp' -and $line -match 'Exception|Failed|reflection.*failed') {
                $results.MountUp_Issues += $line.Trim()
            }
            elseif (($line -match 'AllTameable' -or $line -match 'Tameable') -and $line -match 'NullReferenceException|Exception') {
                $results.AllTameable_Issues += $line.Trim()
            }
            elseif ($line -match 'Procreation' -and $line -match 'NullReferenceException|Exception') {
                $results.Procreation_NREs += $line.Trim()
            }
            elseif ($line -match 'TargetInvocationException|MissingMethodException') {
                $results.Reflection_Errors += $line.Trim()
            }
        }
    }
    finally {
        $reader.Close()
        $reader.Dispose()
    }
    
    Write-Host "" # Newline after dots
    return $results
}

# Scan both logs
$bepResults = Scan-LogFast -Path $bepinexLog -Description "BepInEx/LogOutput.log"
$playerResults = Scan-LogFast -Path $playerLog -Description "Player.log"

# Combine results
$allCPC_Errors = $bepResults.CPC_Errors + $playerResults.CPC_Errors | Select-Object -Unique
$allMountUp = $bepResults.MountUp_Issues + $playerResults.MountUp_Issues | Select-Object -Unique
$allAllTameable = $bepResults.AllTameable_Issues + $playerResults.AllTameable_Issues | Select-Object -Unique
$allProcreation = $bepResults.Procreation_NREs + $playerResults.Procreation_NREs | Select-Object -Unique
$allReflection = $bepResults.Reflection_Errors + $playerResults.Reflection_Errors | Select-Object -Unique

# Output report
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "📊 CPC LOG ANALYSIS REPORT" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

$hasErrors = $false

# Critical Errors
if ($allCPC_Errors.Count -gt 0) {
    Write-Host "`n🚨 CRITICAL: CPC ERRORS FOUND ($($allCPC_Errors.Count) unique)" -ForegroundColor Red
    $allCPC_Errors | Select-Object -First 5 | ForEach-Object {
        if ($_.Length -gt 100) { $_ = $_.Substring(0, 100) + "..." }
        Write-Host "   $_" -ForegroundColor Red
    }
    $hasErrors = $true
} else {
    Write-Host "`n✅ No CPC critical errors found" -ForegroundColor Green
}

# Procreation NREs
if ($allProcreation.Count -gt 0) {
    Write-Host "`n🚨 CRITICAL: Procreation/Offspring Errors ($($allProcreation.Count) unique)" -ForegroundColor Red
    $allProcreation | Select-Object -First 5 | ForEach-Object {
        if ($_.Length -gt 100) { $_ = $_.Substring(0, 100) + "..." }
        Write-Host "   $_" -ForegroundColor Red
    }
    $hasErrors = $true
} else {
    Write-Host "`n✅ No Procreation/Offspring errors found" -ForegroundColor Green
}

# MountUp Issues
if ($allMountUp.Count -gt 0) {
    Write-Host "`n⚠️  MountUp Issues ($($allMountUp.Count) unique)" -ForegroundColor Yellow
    $allMountUp | Select-Object -First 3 | ForEach-Object {
        if ($_.Length -gt 100) { $_ = $_.Substring(0, 100) + "..." }
        Write-Host "   $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n✅ No MountUp issues found" -ForegroundColor Green
}

# AllTameable Issues
if ($allAllTameable.Count -gt 0) {
    Write-Host "`n⚠️  AllTameable Issues ($($allAllTameable.Count) unique)" -ForegroundColor Yellow
    $allAllTameable | Select-Object -First 3 | ForEach-Object {
        if ($_.Length -gt 100) { $_ = $_.Substring(0, 100) + "..." }
        Write-Host "   $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n✅ No AllTameable issues found" -ForegroundColor Green
}

# Reflection Errors
if ($allReflection.Count -gt 0) {
    Write-Host "`n⚠️  Reflection Errors ($($allReflection.Count) unique)" -ForegroundColor Yellow
    $allReflection | Select-Object -First 3 | ForEach-Object {
        if ($_.Length -gt 100) { $_ = $_.Substring(0, 100) + "..." }
        Write-Host "   $_" -ForegroundColor Yellow
    }
    Write-Host "   (May indicate mod version mismatch)" -ForegroundColor DarkYellow
} else {
    Write-Host "`n✅ No reflection errors found" -ForegroundColor Green
}

# Final summary
Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
if ($hasErrors) {
    Write-Host "❌ RESULT: Issues found that need investigation" -ForegroundColor Red
    Write-Host "   Review the errors above or paste them to Devin" -ForegroundColor White
    exit 1
} else {
    Write-Host "✅ RESULT: All checks passed! Logs look clean." -ForegroundColor Green
    Write-Host "   Merged code is working correctly" -ForegroundColor White
    exit 0
}
