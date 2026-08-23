# Probe SAP master data for the CreateSO happy path Quân specified:
#   SalesOrg:      UE00
#   DistrChannel:  WH
#   Division:      AS
#   Customer:      135001
#   Material:      43
#   Plant:         MI00
#
# We probe the four CDS views that drive the CreateSO dropdowns so we can
# tell whether the gap is in our filters or in SAP master data itself.
# Usage: .\test-sap-sodropdowns.ps1

$cred = Get-Credential -UserName "ZAISO_BOT_US" -Message "SAP password for ZAISO_BOT_US"
$base = "https://s40lp1.ucc.cit.tum.de/sap/opu/odata4/sap/zsb_aiso_so_v4/srvd_a2x/sap/zsd_aiso_sales_order/0001"
$qp = "sap-client=324&`$format=json"

function Test-Entity {
    param(
        [string]$Label,
        [string]$Entity,
        [string]$Filter,
        [int]$Top = 5
    )

    $filterPart = ""
    if ($Filter) {
        $filterPart = "&$filter=$Filter"
    }
    $url = "$base/$Entity?$qp`$top=$Top$filterPart"
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

# 1) ValidCustomer for the requested SalesOrg
Test-Entity -Label "ValidCustomer (UE00/WH/AS, top 5)" `
    -Entity "ValidCustomer" `
    -Filter "SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'%20and%20Division%20eq%20'AS'"

# 2) ValidCustomer with Customer eq 0000135001 (any SalesOrg)
Test-Entity -Label "ValidCustomer (Customer eq 0000135001, any org)" `
    -Entity "ValidCustomer" `
    -Filter "Customer%20eq%20'0000135001'"

# 3) ValidCustomer total count for UE00 (using top=200)
Test-Entity -Label "ValidCustomer (UE00, top 200)" `
    -Entity "ValidCustomer" `
    -Filter "SalesOrg%20eq%20'UE00'" `
    -Top 200

# 4) ValidMaterialSales for UE00/WH
Test-Entity -Label "ValidMaterialSales (UE00/WH, top 5)" `
    -Entity "ValidMaterialSales" `
    -Filter "SalesOrg%20eq%20'UE00'%20and%20DistChannel%20eq%20'WH'"

# 5) ValidMaterialSales Material eq 43
Test-Entity -Label "ValidMaterialSales (Material eq 000000000000000043, top 5)" `
    -Entity "ValidMaterialSales" `
    -Filter "Material%20eq%20'000000000000000043'"

# 6) ValidMaterialPlant Material eq 43
Test-Entity -Label "ValidMaterialPlant (Material eq 000000000000000043, top 5)" `
    -Entity "ValidMaterialPlant" `
    -Filter "Material%20eq%20'000000000000000043'"

# 7) SalesArea entries that contain UE00
Test-Entity -Label "SalesArea (SalesOrg eq UE00, top 200)" `
    -Entity "SalesArea" `
    -Filter "SalesOrg%20eq%20'UE00'" `
    -Top 200
