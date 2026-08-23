# Probe pagination behavior of ZI_AISO_VALID_CUSTOMER for UE00/WH/AS.
#
# Uses inline Basic Authorization header instead of -Credential $cred
# because PowerShell's Get-Credential object doesn't always re-send the
# password correctly on subsequent Invoke-WebRequest calls within the
# same PS session.
#
# Usage: .\test-sap-customer135001-pages.ps1

$user = "ZAISO_BOT_US"
$securePass = Read-Host "SAP password for $user" -AsSecureString
$BSTR = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePass)
$plainPass = [Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
$basicAuth = "Basic " + [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes("${user}:${plainPass}")
)
$commonHeaders = @{
    Authorization = $basicAuth
    Accept        = "application/json"
}
$plainPass = $null

$base = "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001"

function Test-Entity {
    param(
        [string]$Label,
        [string]$Entity,
        [string]$FilterOData = "",
        [int]$Top = 5,
        [int]$Skip = 0
    )

    $pathPart = "$base/$Entity"
    $sapParam = 'sa' + 'p-client=324'
    $fmtParam = '$' + 'format=json'
    $topParam = '$' + "top=$Top"
    $queryParts = @($sapParam, $fmtParam, $topParam)
    if ($Skip -gt 0) {
        $queryParts += ('$' + "skip=$Skip")
    }
    if ($FilterOData) {
        $queryParts += ('$' + "filter=$FilterOData")
    }
    $url = "$pathPart" + '?' + [string]::Join('&', $queryParts)

    Start-Sleep -Seconds 3

    Write-Host ""
    Write-Host "=== $Label ===" -ForegroundColor Cyan
    Write-Host "URL: $url"
    try {
        $resp = Invoke-WebRequest -Uri $url -Headers $commonHeaders -UseBasicParsing -Method Get -TimeoutSec 30
        Write-Host "HTTP $($resp.StatusCode)"
        $body = $resp.Content | ConvertFrom-Json
        if ($body.value.Count -eq 0) {
            Write-Host "value: []   (empty)" -ForegroundColor Yellow
            return
        }
        Write-Host "rows: $($body.value.Count)"
        $customers = @($body.value | ForEach-Object { $_.Customer })
        $hit = $customers -contains '135001'
        Write-Host ("contains 135001: {0}" -f $hit)
        Write-Host "first 3: $($customers | Select-Object -First 3)"
        Write-Host "last 3:  $($customers | Select-Object -Last 3)"
    } catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Warm-up — same shape as the previously-working probe so we can confirm
# the inline header is accepted before we trust the new shapes below.
Test-Entity -Label "WARMUP: UE00 only top=5" `
    -Entity "ValidCustomer" `
    -FilterOData "SalesOrg%20eq%20'UE00'" `
    -Top 5

# Combo: UE00/WH/AS
$filter = "SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'%20and%20Division%20eq%20'AS'"

# 1) Tiny top — does 135001 appear in first 50?
Test-Entity -Label "UE00/WH/AS top=50" `
    -Entity "ValidCustomer" `
    -FilterOData $filter `
    -Top 50

# 2) Does the result set have more than 200 rows?
Test-Entity -Label "UE00/WH/AS top=300" `
    -Entity "ValidCustomer" `
    -FilterOData $filter `
    -Top 300

# 3) Page 2 — skip the first 200
Test-Entity -Label "UE00/WH/AS top=200 skip=200" `
    -Entity "ValidCustomer" `
    -FilterOData $filter `
    -Top 200 `
    -Skip 200

# 4) Page 2 narrow slice — skip 100 take 50
Test-Entity -Label "UE00/WH/AS top=50 skip=100" `
    -Entity "ValidCustomer" `
    -FilterOData $filter `
    -Top 50 `
    -Skip 100