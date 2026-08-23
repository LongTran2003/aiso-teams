# Probe SAP master data for the CreateSO happy path Quân specified:
#   SalesOrg:      UE00
#   DistrChannel:  WH
#   Division:      AS
#   Customer:      135001
#   Material:      43
#   Plant:         MI00
#
# Run on a host with access to s40lp1 to confirm whether the gap is in
# our filters or in SAP master data itself.
# Usage: .\test-sap-sodropdowns.ps1

$cred = Get-Credential -UserName "ZAISO_BOT_US" -Message "SAP password for ZAISO_BOT_US"
$base = "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001"

function Test-Entity {
    param(
        [string]$Label,
        [string]$Entity,
        [string]$FilterOData = "",
        [int]$Top = 5
    )

    # The '?' separator between path and query is mandatory for the
    # SAP gateway; previous revisions joined with '&' and got 400s.
    # Use named vars for the literal tokens so PSReadLine on the user's
    # host can't substitute them at parse time.
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
        } else {
            Write-Host "rows: $($body.value.Count)"
            $body.value | Select-Object -First $Top | ConvertTo-Json -Depth 5 | Write-Host
        }
    } catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# 1) ValidCustomer for the requested SalesArea combo
Test-Entity -Label "ValidCustomer (UE00/WH/AS, top 5)" `
    -Entity "ValidCustomer" `
    -FilterOData "SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'%20and%20Division%20eq%20'AS'"

# 2) ValidCustomer with Customer eq 0000135001 (any SalesOrg)
Test-Entity -Label "ValidCustomer (Customer eq 0000135001, any org, top 5)" `
    -Entity "ValidCustomer" `
    -FilterOData "Customer%20eq%20'0000135001'"

# 3) ValidCustomer for UE00 only (top 200)
Test-Entity -Label "ValidCustomer (UE00 only, top 200)" `
    -Entity "ValidCustomer" `
    -FilterOData "SalesOrg%20eq%20'UE00'" `
    -Top 200

# 4) ValidMaterialSales for UE00/WH
Test-Entity -Label "ValidMaterialSales (UE00/WH, top 5)" `
    -Entity "ValidMaterialSales" `
    -FilterOData "SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'"

# 5) ValidMaterialSales Material eq 000000000000000043
Test-Entity -Label "ValidMaterialSales (Material eq 000000000000000043, top 5)" `
    -Entity "ValidMaterialSales" `
    -FilterOData "Material%20eq%20'000000000000000043'"

# 6) ValidMaterialPlant Material eq 000000000000000043
Test-Entity -Label "ValidMaterialPlant (Material eq 000000000000000043, top 5)" `
    -Entity "ValidMaterialPlant" `
    -FilterOData "Material%20eq%20'000000000000000043'"

# 7) SalesArea for UE00 (top 200) — verifies the previous fix
Test-Entity -Label "SalesArea (SalesOrg eq UE00, top 200)" `
    -Entity "SalesArea" `
    -FilterOData "SalesOrg%20eq%20'UE00'" `
    -Top 200