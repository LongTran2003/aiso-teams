@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO KPI - By Product'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZI_AISO_KPI_BY_PRODUCT
  as select from vbap
  inner join vbak
    on vbap.vbeln = vbak.vbeln
  association [0..1] to makt as _Material
    on  $projection.Material = _Material.matnr
    and _Material.spras      = 'E'
{
  key vbap.matnr                                     as Material,
      _Material.maktx                                as MaterialName,
      vbak.vkorg                                      as SalesOrg,
      vbap.waerk                                      as Currency,
      vbap.vrkme                                      as Unit,

      @Semantics.amount.currencyCode: 'Currency'
      cast( sum( vbap.netwr ) as abap.curr( 15, 2 ) )  as TotalRevenue,

      @Semantics.quantity.unitOfMeasure: 'Unit'
      cast( sum( vbap.kwmeng ) as abap.quan( 15, 3 ) ) as TotalQty,

      count( distinct vbap.vbeln )                    as OrderCount
}
group by vbap.matnr, _Material.maktx, vbak.vkorg, vbap.waerk, vbap.vrkme
