$ErrorActionPreference = 'Stop'

$ApiPort = 5079
$ChromaPort = 8039
$Root = Split-Path -Parent $PSScriptRoot
$ChromaExe = (Get-Command chroma).Source
$DotnetExe = (Get-Command dotnet).Source
$SmokeRoot = Join-Path $env:TEMP ("ikc-m8-smoke-" + [guid]::NewGuid().ToString('N'))
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

    $ReviewerLogin = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body '{"email":"reviewer@example.local","password":"ChangeMe123!"}'

    $ReviewerToken = $ReviewerLogin.accessToken
    $ReviewerHeaders = @{ Authorization = "Bearer $ReviewerToken" }

    $Folder = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/folders" `
        -Method Post `
        -Headers $ReviewerHeaders `
        -ContentType 'application/json' `
        -Body '{"parentId":null,"name":"M8 Smoke"}'

    $SamplePath = Join-Path $SmokeRoot 'sample.txt'
    Set-Content `
        -LiteralPath $SamplePath `
        -Value 'Payment error workflow: check transaction logs, verify provider response code, then retry the payment once after customer confirmation.' `
        -Encoding UTF8

    $UploadJson = & curl.exe `
        -sS `
        -X POST "http://localhost:$ApiPort/api/documents" `
        -H "Authorization: Bearer $ReviewerToken" `
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
        -Headers $ReviewerHeaders `
        -ContentType 'application/json' `
        -Body "{`"versionId`":`"$VersionId`"}" | Out-Null

    $FinalStatus = $null
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        $Detail = Invoke-RestMethod `
            -Uri "http://localhost:$ApiPort/api/documents/$($Upload.id)" `
            -Method Get `
            -Headers $ReviewerHeaders

        $FinalStatus = $Detail.versions[0].status
        if ($FinalStatus -eq 'Indexed' -or $FinalStatus -eq 'ProcessingFailed') {
            break
        }
    }

    if ($FinalStatus -ne 'Indexed') {
        throw "Expected Indexed before dashboard smoke, got $FinalStatus"
    }

    $AskBody = @{
        question = 'payment error workflow'
        scopeType = 'All'
        folderId = $null
        documentId = $null
    } | ConvertTo-Json

    $Answer = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/ai/ask" `
        -Method Post `
        -Headers $ReviewerHeaders `
        -ContentType 'application/json' `
        -Body $AskBody

    Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/ai/interactions/$($Answer.interactionId)/feedback" `
        -Method Post `
        -Headers $ReviewerHeaders `
        -ContentType 'application/json' `
        -Body '{"value":"Incorrect","note":"Dashboard smoke feedback."}' | Out-Null

    $Draft = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/wiki/generate" `
        -Method Post `
        -Headers $ReviewerHeaders `
        -ContentType 'application/json' `
        -Body "{`"documentId`":`"$($Upload.id)`",`"documentVersionId`":`"$VersionId`"}"

    Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/wiki/drafts/$($Draft.id)/publish" `
        -Method Post `
        -Headers $ReviewerHeaders `
        -ContentType 'application/json' `
        -Body "{`"visibilityScope`":`"Folder`",`"folderId`":`"$($Folder.id)`",`"isCompanyPublicConfirmed`":false}" | Out-Null

    $Summary = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/dashboard/summary" `
        -Method Get `
        -Headers $ReviewerHeaders

    $AdminLogin = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body '{"email":"admin@example.local","password":"ChangeMe123!"}'

    $AuditLogs = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/audit-logs" `
        -Method Get `
        -Headers @{ Authorization = "Bearer $($AdminLogin.accessToken)" }

    $HasDocumentApproved = @($AuditLogs) | Where-Object { $_.action -eq 'DocumentApproved' } | Select-Object -First 1
    $HasWikiPublished = @($AuditLogs) | Where-Object { $_.action -eq 'WikiPublished' } | Select-Object -First 1

    [pscustomobject]@{
        DocumentId = $Upload.id
        AiQuestionCount = $Summary.aiQuestionCount
        FeedbackIncorrectCount = $Summary.feedbackIncorrectCount
        IncorrectPendingCount = $Summary.incorrectFeedbackPendingCount
        TopCitedCount = @($Summary.topCitedSources).Count
        AuditLogCount = @($AuditLogs).Count
        HasDocumentApproved = [bool]$HasDocumentApproved
        HasWikiPublished = [bool]$HasWikiPublished
        SmokeRoot = $SmokeRoot
    } | ConvertTo-Json

    if ($Summary.aiQuestionCount -lt 1 -or $Summary.feedbackIncorrectCount -lt 1 -or $Summary.incorrectFeedbackPendingCount -lt 1 -or @($Summary.topCitedSources).Count -lt 1 -or -not $HasDocumentApproved -or -not $HasWikiPublished) {
        Write-Host '--- API LOG TAIL ---'
        Get-Content $ApiLog -Tail 220
        Write-Host '--- API ERR TAIL ---'
        Get-Content $ApiErr -Tail 220 -ErrorAction SilentlyContinue
        throw 'Expected populated dashboard summary and audit logs.'
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
