# Stores local secrets for `dotnet run` in user-secrets (outside the repo). Run once per machine:
#   pwsh backend/scripts/dev-secrets.ps1
# Reads POSTGRES_PASSWORD / SEED_ADMIN_* from ../../.env when present, so it matches docker compose.
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
$api = "$root/backend/src/Maydel.Api"

$envFile = @{}
if (Test-Path "$root/.env") {
    Get-Content "$root/.env" | Where-Object { $_ -match '^\s*([A-Z_]+)=(.*)$' } | ForEach-Object { $envFile[$Matches[1]] = $Matches[2] }
}
$dbPassword = $envFile['POSTGRES_PASSWORD'] ?? 'maydel_dev_only'
$dbPort = $envFile['POSTGRES_PORT'] ?? '5432'
$adminEmail = $envFile['SEED_ADMIN_EMAIL'] ?? 'admin@maydel.test'
$adminPassword = $envFile['SEED_ADMIN_PASSWORD'] ?? 'Demo#Pass2026'
$signingKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

dotnet user-secrets set --project $api "ConnectionStrings:Default" "Host=localhost;Port=$dbPort;Database=maydel;Username=maydel;Password=$dbPassword" | Out-Null
dotnet user-secrets set --project $api "Jwt:SigningKey" $signingKey | Out-Null
dotnet user-secrets set --project $api "Seed:AdminEmail" $adminEmail | Out-Null
dotnet user-secrets set --project $api "Seed:AdminPassword" $adminPassword | Out-Null
if ($envFile['GOOGLE_CLIENT_ID']) {
    dotnet user-secrets set --project $api "Authentication:Google:ClientId" $envFile['GOOGLE_CLIENT_ID'] | Out-Null
}
Write-Host "User secrets set for Maydel.Api. Start Postgres with 'docker compose up -d db', then 'dotnet run --project backend/src/Maydel.Api'."
