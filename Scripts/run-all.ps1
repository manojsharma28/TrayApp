param(
    [string]$ConfigPath = "Config/appRegistry.json"
)

# Build solution
dotnet build TrayAppManager.sln

# Ensure logs folder
$logs = Join-Path -Path (Get-Location) -ChildPath "Logs"
if (-not (Test-Path $logs)) { New-Item -ItemType Directory -Path $logs | Out-Null }

Write-Host "Starting TrayApp.Core"
$pids = @()
$proc = Start-Process -FilePath dotnet -ArgumentList 'run','--project','src/TrayApp.Core' -NoNewWindow -RedirectStandardOutput "$logs\core.log" -RedirectStandardError "$logs\core.err.log" -PassThru
if ($proc) { $pids += @{ name = 'TrayApp.Core'; pid = $proc.Id; cmd = 'dotnet run --project src/TrayApp.Core' } }
Start-Sleep -Seconds 1

Write-Host "Starting TrayApp.UI"
$proc = Start-Process -FilePath dotnet -ArgumentList 'run','--project','src/TrayApp.UI' -NoNewWindow -RedirectStandardOutput "$logs\ui.log" -RedirectStandardError "$logs\ui.err.log" -PassThru
if ($proc) { $pids += @{ name = 'TrayApp.UI'; pid = $proc.Id; cmd = 'dotnet run --project src/TrayApp.UI' } }
Start-Sleep -Seconds 1

Write-Host "Reading config: $ConfigPath"
$cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json

foreach ($app in $cfg.apps)
{
    $id = $app.appId
    $cmd = $app.startCommand
    Write-Host "Starting app $id -> $cmd"
    $proc = Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile','-Command',$cmd -NoNewWindow -RedirectStandardOutput "$logs\$($id).log" -RedirectStandardError "$logs\$($id).err.log" -PassThru
    if ($proc) { $pids += @{ name = $id; pid = $proc.Id; cmd = $cmd } }
}

Write-Host "All processes started. Logs in $logs"

# Save PIDs for stop script
$pidFile = Join-Path $logs 'pids.json'
$pids | ConvertTo-Json -Depth 5 | Out-File -FilePath $pidFile -Encoding UTF8
Write-Host "PIDs recorded to $pidFile"
