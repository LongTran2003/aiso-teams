# Probe pagination behavior of ZI_AISO_VALID_CUSTOMER for UE00/WH/AS:
#   - top=300 to see whether the result set really has more than 200 rows
#   - top=200 skip=200 to see whether the next page contains 135001
#   - top=50  to see whether 135001 is in the first 50
#
# Usage: .\test-sap-customer135001-pages.ps1

$cred = Get-Credential -UserName "ZAISO_BOT_US" -Message "SAP password for ZAISO_BOT_US"
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

    Write-Host ""
    Write-Host "=== $Label ===" -ForegroundColor Cyan
    Write-Host "URL: $url"
    try {
        $resp = Invoke-WebRequest -Uri $url -Credential $cred -UseBasicParsing -Method Get
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

# 4) Page 2 narrow slice — skip 100 take 50 (try to land on 135001 if it sits there)
Test-Entity -Label "UE00/WH/AS top=50 skip=100" `
    -Entity "ValidCustomer" `
    -FilterOData $filter `
    -Top 50 `
    -Skip 100