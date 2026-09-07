#!/usr/bin/env bash
CONFIG=${1:-Config/appRegistry.json}
ROOT=$(pwd)
mkdir -p "$ROOT/Logs"

echo "Building solution..."
dotnet build TrayAppManager.sln

echo "Starting TrayApp.Core"
dotnet run --project src/TrayApp.Core > "$ROOT/Logs/core.log" 2> "$ROOT/Logs/core.err.log" &
sleep 1

echo "Starting TrayApp.UI"
dotnet run --project src/TrayApp.UI > "$ROOT/Logs/ui.log" 2> "$ROOT/Logs/ui.err.log" &
sleep 1

echo "Reading config: $CONFIG"
APPS=$(jq -c '.apps[]' "$CONFIG")
for a in $APPS; do
  id=$(echo $a | jq -r '.appId')
  cmd=$(echo $a | jq -r '.startCommand')
  echo "Starting $id -> $cmd"
  bash -c "$cmd" > "$ROOT/Logs/$id.log" 2> "$ROOT/Logs/$id.err.log" &
done

echo "All processes started. Logs in $ROOT/Logs"
