@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Basic View - SO Header'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZI_AISO_SO_HEADER
  as select from vbak
{
  key vbak.vbeln                          as SoNumber,
      vbak.auart                          as DocType,
      vbak.kunnr                          as Customer,
      vbak.vkorg                          as SalesOrg,
      vbak.vtweg                          as DistChannel,
      vbak.spart                          as Division,
      vbak.waerk                          as Currency,
      @Semantics.amount.currencyCode: 'Currency'
      vbak.netwr                          as NetValue,
      vbak.audat                          as DocDate,
      vbak.ernam                          as CreatedBy,
      vbak.erdat                          as CreatedDate,
      vbak.gbstk                          as OverallStatus
}
