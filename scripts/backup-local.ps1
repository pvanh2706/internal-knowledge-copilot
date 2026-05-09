param(
    [string]$DatabasePath = './data/internal-knowledge-copilot.db',
    [string]$StoragePath = './storage',
    [string]$BackupRoot = './backups'
)

$ErrorActionPreference = 'Stop'

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRootItem = New-Item -ItemType Directory -Force -Path $BackupRoot
$resolvedBackupRoot = Resolve-Path -LiteralPath $backupRootItem.FullName
$backupDir = Join-Path $resolvedBackupRoot.Path "backup-$timestamp"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

$resolvedDatabasePath = Resolve-Path -Path $DatabasePath -ErrorAction SilentlyContinue
if ($resolvedDatabasePath) {
    Copy-Item -LiteralPath $resolvedDatabasePath.Path -Destination (Join-Path $backupDir 'internal-knowledge-copilot.db') -Force
}

$resolvedStoragePath = Resolve-Path -Path $StoragePath -ErrorAction SilentlyContinue
if ($resolvedStoragePath) {
    Compress-Archive -Path (Join-Path $resolvedStoragePath.Path '*') -DestinationPath (Join-Path $backupDir 'storage.zip') -Force
}

[pscustomobject]@{
    BackupPath = $backupDir
    DatabaseIncluded = [bool]$resolvedDatabasePath
    StorageIncluded = [bool]$resolvedStoragePath
    VectorIndexStrategy = 'Rebuild ChromaDB from approved document versions and published wiki pages.'
} | ConvertTo-Json
