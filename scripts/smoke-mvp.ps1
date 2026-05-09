$ErrorActionPreference = 'Stop'

$ApiPort = 5080
$ChromaPort = 8040
$Root = Split-Path -Parent $PSScriptRoot
$ChromaExe = (Get-Command chroma).Source
$DotnetExe = (Get-Command dotnet).Source
$SmokeRoot = Join-Path $env:TEMP ("ikc-mvp-smoke-" + [guid]::NewGuid().ToString('N'))
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

function Login($email, $password) {
    Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body (@{ email = $email; password = $password } | ConvertTo-Json)
}

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
            Login 'admin@example.local' 'ChangeMe123!' | Out-Null
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

    $AdminLogin = Login 'admin@example.local' 'ChangeMe123!'
    $AdminHeaders = @{ Authorization = "Bearer $($AdminLogin.accessToken)" }

    $Teams = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/teams" `
        -Method Get `
        -Headers $AdminHeaders
    $TeamPermissionsJson = @{
        teamPermissions = @($Teams | ForEach-Object {
            @{ teamId = $_.id; canView = $true }
        })
    } | ConvertTo-Json -Depth 4

    $Folder = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/folders" `
        -Method Post `
        -Headers $AdminHeaders `
        -ContentType 'application/json' `
        -Body '{"parentId":null,"name":"MVP Smoke"}'

    Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/folders/$($Folder.id)/permissions" `
        -Method Put `
        -Headers $AdminHeaders `
        -ContentType 'application/json' `
        -Body $TeamPermissionsJson | Out-Null

    $UserLogin = Login 'user@example.local' 'ChangeMe123!'
    $UserToken = $UserLogin.accessToken
    $UserHeaders = @{ Authorization = "Bearer $UserToken" }

    $SamplePath = Join-Path $SmokeRoot 'sample.txt'
    Set-Content `
        -LiteralPath $SamplePath `
        -Value 'Payment error workflow: check transaction logs, verify provider response code, then retry the payment once after customer confirmation.' `
        -Encoding UTF8

    $UploadJson = & curl.exe `
        -sS `
        -X POST "http://localhost:$ApiPort/api/documents" `
        -H "Authorization: Bearer $UserToken" `
        -F "FolderId=$($Folder.id)" `
        -F "Title=Payment Error Workflow" `
        -F "Description=MVP smoke test" `
        -F "File=@$SamplePath;type=text/plain"

    if ($LASTEXITCODE -ne 0) {
        throw "curl upload failed with code $LASTEXITCODE"
    }

    $Upload = $UploadJson | ConvertFrom-Json
    if (@($Upload.versions).Count -lt 1) {
        throw "Upload did not return a document version. Raw response: $UploadJson"
    }

    $VersionId = $Upload.versions[0].id

    $ReviewerLogin = Login 'reviewer@example.local' 'ChangeMe123!'
    $ReviewerHeaders = @{ Authorization = "Bearer $($ReviewerLogin.accessToken)" }

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
            -Headers $UserHeaders

        $FinalStatus = $Detail.versions[0].status
        if ($FinalStatus -eq 'Indexed' -or $FinalStatus -eq 'ProcessingFailed') {
            break
        }
    }

    if ($FinalStatus -ne 'Indexed') {
        throw "Expected Indexed before MVP smoke AI flow, got $FinalStatus"
    }

    $AskBody = @{
        question = 'payment error workflow'
        scopeType = 'Folder'
        folderId = $Folder.id
        documentId = $null
    } | ConvertTo-Json

    $Answer = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/ai/ask" `
        -Method Post `
        -Headers $UserHeaders `
        -ContentType 'application/json' `
        -Body $AskBody

    Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/ai/interactions/$($Answer.interactionId)/feedback" `
        -Method Post `
        -Headers $UserHeaders `
        -ContentType 'application/json' `
        -Body '{"value":"Incorrect","note":"MVP smoke feedback."}' | Out-Null

    $IncorrectQueue = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/feedback/incorrect" `
        -Method Get `
        -Headers $ReviewerHeaders

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

    $AnswerAfterWiki = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/ai/ask" `
        -Method Post `
        -Headers $UserHeaders `
        -ContentType 'application/json' `
        -Body $AskBody

    $Summary = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/dashboard/summary" `
        -Method Get `
        -Headers $ReviewerHeaders

    $AuditLogs = Invoke-RestMethod `
        -Uri "http://localhost:$ApiPort/api/audit-logs" `
        -Method Get `
        -Headers $AdminHeaders

    $FirstCitationType = if (@($AnswerAfterWiki.citations).Count -gt 0) { $AnswerAfterWiki.citations[0].sourceType } else { $null }
    $HasPermissionChanged = @($AuditLogs) | Where-Object { $_.action -eq 'PermissionChanged' } | Select-Object -First 1

    [pscustomobject]@{
        DocumentId = $Upload.id
        InitialCitationCount = @($Answer.citations).Count
        IncorrectQueueCount = @($IncorrectQueue).Count
        FirstCitationTypeAfterWiki = $FirstCitationType
        DashboardAiQuestions = $Summary.aiQuestionCount
        AuditLogCount = @($AuditLogs).Count
        HasPermissionChangedAudit = [bool]$HasPermissionChanged
        SmokeRoot = $SmokeRoot
    } | ConvertTo-Json

    if (@($Answer.citations).Count -lt 1 -or @($IncorrectQueue).Count -lt 1 -or $FirstCitationType -ne 'Wiki' -or $Summary.aiQuestionCount -lt 2 -or -not $HasPermissionChanged) {
        Write-Host '--- API LOG TAIL ---'
        Get-Content $ApiLog -Tail 240
        Write-Host '--- API ERR TAIL ---'
        Get-Content $ApiErr -Tail 240 -ErrorAction SilentlyContinue
        throw 'Expected complete MVP smoke flow to pass.'
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
