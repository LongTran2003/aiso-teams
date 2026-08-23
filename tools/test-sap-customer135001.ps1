# Verify 135001 sits inside the UE00/WH/AS combo (the CreateSO happy path)
# and locate where it lands under different $top values.
#
# Usage: .\test-sap-customer135001.ps1

$cred = Get-Credential -UserName "ZAISO_BOT_US" -Message "SAP password for ZAISO_BOT_US"
$base = "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001"

function Test-Entity {
    param(
        [string]$Label,
        [string]$Entity,
        [string]$FilterOData = "",
        [int]$Top = 5
    )

    $pathPart = "$base/$Entity"
    $sapParam = 'sa' + 'p-client=324'
    $fmtParam = '$' + 'format=json'
    $topParam = '$' + "top=$Top"
    $queryParts = @($sapParam, $fmtParam, $topParam)
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
        $customers = $body.value | ForEach-Object { $_.Customer }
        Write-Host "min Customer: $($customers | Measure-Object -Minimum).Minimum"
        Write-Host "max Customer: $($customers | Measure-Object -Maximum).Maximum"
        Write-Host "first 5: $($customers | Select-Object -First 5)"
        Write-Host "last 5:  $($customers | Select-Object -Last 5)"
        $hit = $customers -contains '135001'
        Write-Host ("contains 135001: {0}" -f $hit)
        if ($hit) {
            $row = $body.value | Where-Object { $_.Customer -eq '135001' } | Select-Object -First 1
            $row | ConvertTo-Json -Depth 5 | Write-Host
        }
    } catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Q1: Does 135001 sit in UE00/WH/AS?
Test-Entity -Label "Q1: 135001 in UE00/WH/AS" `
    -Entity "ValidCustomer" `
    -FilterOData "Customer%20eq%20'0000135001'%20and%20SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'%20and%20Division%20eq%20'AS'"

# Q2: Where does 135001 sit under top=100 of UE00/WH/AS?
Test-Entity -Label "Q2: UE00/WH/AS top=100" `
    -Entity "ValidCustomer" `
    -FilterOData "SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'%20and%20Division%20eq%20'AS'" `
    -Top 100

# Q3: Same combo top=200 (catch any cut-off).
Test-Entity -Label "Q3: UE00/WH/AS top=200" `
    -Entity "ValidCustomer" `
    -FilterOData "SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'%20and%20Division%20eq%20'AS'" `
    -Top 200