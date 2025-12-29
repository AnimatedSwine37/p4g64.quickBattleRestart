# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p4g64.quickBattleRestart/*" -Force -Recurse
dotnet publish "./p4g64.quickBattleRestart.csproj" -c Release -o "$env:RELOADEDIIMODS/p4g64.quickBattleRestart" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location