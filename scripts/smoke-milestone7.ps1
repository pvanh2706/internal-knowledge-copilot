$ErrorActionPreference = 'Stop'

$ApiPort = 5078
$ChromaPort = 8038
$Root = Split-Path -Parent $PSScriptRoot
$ChromaExe = (Get-Command chroma).Source
$DotnetExe = (Get-Command dotnet).Source
$SmokeRoot = Join-Path $env:TEMP ("ikc-m7-smoke-" + [guid]::NewGuid().ToString('N'))
$DbDir = Join-Path $SmokeRoot 'data'
$DbPath = Join-Path $DbDir 'smoke.db'
$StoragePath = Join-Path $SmokeRoot 'storage'
$ChromaPath = Join-Path $SmokeRoot 'chroma'
$LogsPath = Join-Path $SmokeRoot 'logs'

New-Item -ItemType Directory -Force -Path $DbDir | Out-Null
New-Item -ItemType Directory -Force -Path $StoragePath | Out-Null
New-Item -ItemType Directory -Force -Path $ChromaPath | Out-Null
New-Item -ItemType Directory -Force -Path $LogsPath | Out-Null

$ApiLog = Join-Path $LogsPath 'api.log'
$ApiErr = Join-Path $LogsPath 'api.err.log'
$ChromaLog = Join-Path $LogsPath 'chroma.log'
$ChromaErr = Join-Path $LogsPath 'chroma.err.log'
$ApiProc = $null
$ChromaProc = $null

try {
    $ChromaProc = Start-Process `
        -FilePath $ChromaExe `
        -ArgumentList @('run', '--host', 'localhost', '--port', "$ChromaPort", '--path', $ChromaPath) `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $ChromaLog `
        -RedirectStandardError $ChromaErr

    $ChromaReady = $false
    for ($i = 0; $i -lt 40; $i++) {
        try {
            Invoke-RestMethod -Uri "http://localhost:$ChromaPort/api/v2/heartbeat" -Method Get | Out-Null
            $ChromaReady = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $ChromaReady) {
        throw 'Chroma did not become ready.'
    }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Database__SqlitePath = $DbPath
    $env:Storage__RootPath = $StoragePath
    $env:Chroma__BaseUrl = "http://localhost:$ChromaPort"
    $env:BackgroundJobs__PollSeconds = '1'

    $ApiProc = Start-Process `
        -FilePath $DotnetExe `
        -ArgumentList @('run', '--no-build', '--project', 'src/backend/InternalKnowledgeCopilot.Api/InternalKnowledgeCopilot.Api.csproj', '--urls', "http://localhost:$ApiPort") `
        -WorkingDirectory $Root `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $ApiLog `
        -RedirectStandardError $ApiErr

    $ApiReady = $false
    for ($i = 0; $i -lt 60; $i++) {
        try {
            Invoke-RestMethod `
                -Uri "http://localhost:$ApiPort/api/auth/login" `
                -Method Post `
                -ContentType 'application/json' `
                -Body '{"email":"reviewer@example.local","password":"ChangeMe123!"}' | Out-Null
            $ApiReady = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $ApiReady) {
        throw 'API did not become ready.'
    }

    $Login = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body '{"email":"reviewer@example.local","password":"ChangeMe123!"}'

    $Token = $Login.accessToken
    $Headers = @{ Authorization = "Bearer $Token" }

    $Folder = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/folders" `
        -Method Post `
        -Headers $Headers `
        -ContentType 'application/json' `
        -Body '{"parentId":null,"name":"M7 Smoke"}'

    $SamplePath = Join-Path $SmokeRoot 'sample.txt'
    Set-Content `
        -LiteralPath $SamplePath `
        -Value 'Payment error workflow: check transaction logs, verify provider response code, then retry the payment once after customer confirmation.' `
        -Encoding UTF8

    $UploadJson = & curl.exe `
        -sS `
        -X POST "http://localhost:$ApiPort/api/documents" `
        -H "Authorization: Bearer $Token" `
        -F "FolderId=$($Folder.id)" `
        -F "Title=Payment Error Workflow" `
        -F "Description=Smoke test" `
        -F "File=@$SamplePath;type=text/plain"

    if ($LASTEXITCODE -ne 0) {
        throw "curl upload failed with code $LASTEXITCODE"
    }

    $Upload = $UploadJson | ConvertFrom-Json
    $VersionId = $Upload.versions[0].id

    Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/documents/$($Upload.id)/approve" `
        -Method Post `
        -Headers $Headers `
        -ContentType 'application/json' `
        -Body "{`"versionId`":`"$VersionId`"}" | Out-Null

    $FinalStatus = $null
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        $Detail = Invoke-RestMethod `
            -Uri "http://localhost:$ApiPort/api/documents/$($Upload.id)" `
            -Method Get `
            -Headers $Headers

        $FinalStatus = $Detail.versions[0].status
        if ($FinalStatus -eq 'Indexed' -or $FinalStatus -eq 'ProcessingFailed') {
            break
        }
    }

    if ($FinalStatus -ne 'Indexed') {
        throw "Expected Indexed before generating wiki, got $FinalStatus"
    }

    $Draft = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/wiki/generate" `
        -Method Post `
        -Headers $Headers `
        -ContentType 'application/json' `
        -Body "{`"documentId`":`"$($Upload.id)`",`"documentVersionId`":`"$VersionId`"}"

    $Page = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/wiki/drafts/$($Draft.id)/publish" `
        -Method Post `
        -Headers $Headers `
        -ContentType 'application/json' `
        -Body "{`"visibilityScope`":`"Folder`",`"folderId`":`"$($Folder.id)`",`"isCompanyPublicConfirmed`":false}"

    $AskBody = @{
        question = 'payment error workflow'
        scopeType = 'All'
        folderId = $null
        documentId = $null
    } | ConvertTo-Json

    $Answer = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/ai/ask" `
        -Method Post `
        -Headers $Headers `
        -ContentType 'application/json' `
        -Body $AskBody

    $FirstCitationType = if (@($Answer.citations).Count -gt 0) { $Answer.citations[0].sourceType } else { $null }
    [pscustomobject]@{
        DocumentId = $Upload.id
        DraftId = $Draft.id
        DraftStatus = $Draft.status
        PageId = $Page.id
        PageVisibility = $Page.visibilityScope
        FirstCitationType = $FirstCitationType
        CitationCount = @($Answer.citations).Count
        SmokeRoot = $SmokeRoot
    } | ConvertTo-Json

    if ($Draft.status -ne 'Draft' -or $Page.visibilityScope -ne 'Folder' -or $FirstCitationType -ne 'Wiki') {
        Write-Host '--- API LOG TAIL ---'
        Get-Content $ApiLog -Tail 220
        Write-Host '--- API ERR TAIL ---'
        Get-Content $ApiErr -Tail 220 -ErrorAction SilentlyContinue
        throw 'Expected draft, published wiki page, and Wiki citation priority.'
    }
}
finally {
    if ($ApiProc -and -not $ApiProc.HasExited) {
        Stop-Process -Id $ApiProc.Id -Force -ErrorAction SilentlyContinue
    }

    if ($ChromaProc -and -not $ChromaProc.HasExited) {
        Stop-Process -Id $ChromaProc.Id -Force -ErrorAction SilentlyContinue
    }

    Remove-Item Env:\Database__SqlitePath -ErrorAction SilentlyContinue
    Remove-Item Env:\Storage__RootPath -ErrorAction SilentlyContinue
    Remove-Item Env:\Chroma__BaseUrl -ErrorAction SilentlyContinue
    Remove-Item Env:\BackgroundJobs__PollSeconds -ErrorAction SilentlyContinue
}
