param(
    [string]$PidFile = "Logs/pids.json",
    [string]$ConfigPath = "Config/appRegistry.json"
)

Write-Host "Stopping processes listed in $PidFile (if present)"
if (Test-Path $PidFile) {
    try {
        $raw = Get-Content $PidFile -Raw
        $entries = ConvertFrom-Json $raw
        foreach ($e in $entries) {
            try {
                $id = $e.name
                $pid = [int]$e.pid
                Write-Host "Stopping $id (PID $pid)"
                Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
            } catch {
                Write-Warning "Failed to stop entry: $_"
            }
        }
    } catch {
        Write-Warning "Could not parse $PidFile: $_"
    }
    try { Remove-Item $PidFile -ErrorAction SilentlyContinue } catch {}
}
else {
    Write-Host "$PidFile not found, attempting best-effort stop using config"
}

if (Test-Path $ConfigPath) {
    $cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    foreach ($app in $cfg.apps) {
        $id = $app.appId
        $cmd = $app.startCommand
        if (-not [string]::IsNullOrWhiteSpace($cmd)) {
            # Attempt to find process by command line
            $procs = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and $_.CommandLine -like "*${cmd}*" }
            foreach ($p in $procs) {
                try { Write-Host "Killing process $($p.ProcessId) matching $cmd"; Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue } catch {}
            }
        }
        else {
            # Try by executable field
            if ($app.executable -or $app.executablePath) {
                $exec = $app.executable ?? $app.executablePath
                $name = [System.IO.Path]::GetFileNameWithoutExtension($exec)
                $ps = Get-Process -Name $name -ErrorAction SilentlyContinue
                foreach ($p in $ps) { try { Write-Host "Killing $($p.Id) $name"; Stop-Process -Id $p.Id -Force } catch {} }
            }
        }
    }
}

# Also attempt to stop tray and core dotnet runs by matching project paths
$dotnetProcs = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and ($_.CommandLine -like "*src/TrayApp.Core*" -or $_.CommandLine -like "*src\TrayApp.Core*" -or $_.CommandLine -like "*src/TrayApp.UI*" -or $_.CommandLine -like "*src\TrayApp.UI*") }
foreach ($p in $dotnetProcs) { try { Write-Host "Stopping dotnet PID $($p.ProcessId)"; Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue } catch {} }

Write-Host "Stop attempts complete. Check Logs for details."
