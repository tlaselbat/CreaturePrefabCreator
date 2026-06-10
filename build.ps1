$projectDir = "C:\Users\16265\DevelopmentProjects\ValheimModding\CreaturePrefabCreator"
$pluginDir = "C:\Users\16265\DevelopmentProjects\ValheimModding\LaunchValheim\ModTestingProfile\BepInEx\plugins"

$valheim = Get-Process -Name "valheim" -ErrorAction SilentlyContinue
if ($valheim) {
    Write-Host "Closing Valheim..."
    $valheim | Stop-Process -Force
    $valheim | Wait-Process
    Start-Sleep -Seconds 1
    if (Get-Process -Name "valheim" -ErrorAction SilentlyContinue) {
        Write-Error "Valheim is still running. Aborting."
        exit 1
    }
    Write-Host "Valheim closed."
}

$buildOutput = & dotnet build "$projectDir\CreaturePrefabCreator.csproj" -c Debug 2>&1
$buildExitCode = $LASTEXITCODE
$buildOutput | Select-Object -First 50 | ForEach-Object { Write-Host $_ }

if ($buildExitCode -eq 0) {
    Copy-Item -Path "$projectDir\bin\Debug\CreaturePrefabCreator.dll" -Destination $pluginDir -Force
    Write-Host "Copied CreaturePrefabCreator.dll to $pluginDir"

    Write-Host "Starting Valheim through Steam..."

    $valheimExe = "C:\Users\16265\DevelopmentProjects\ValheimModding\LaunchValheim\ModTestingProfile\valheim.exe"
    $launchOptions = @("-console")
        # ("-console", "+connect", "192.168.1.175:2456", "+password", "tablex")
    if (Test-Path $valheimExe) {
        # Launch directly with SteamAppId so Steam tracks it + overlay works
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $valheimExe
        $psi.Arguments = $launchOptions -join " "
        $psi.WorkingDirectory = Split-Path $valheimExe
        $psi.EnvironmentVariables["SteamAppId"] = "892970"
        [System.Diagnostics.Process]::Start($psi) | Out-Null
        Write-Host "Valheim launched."
    } else {
        Write-Error "Could not find valheim.exe at: $valheimExe"
    }
} else {
    Write-Host "Build failed; skipping copy."
}