@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Basic View - SO Header'
@Metadata.ignorePropagatedAnnotations: true
@ObjectModel.usageType: { serviceQuality: #X, sizeCategory: #S, dataClass: #MIXED }

define root view entity ZI_AISO_SO_HEADER
  as select from vbak
  association [0..*] to ZI_AISO_SO_ITEM as _Items
    on $projection.SoNumber = _Items.SoNumber
{
  @ObjectModel.foreignKey.association: null 
  @ObjectModel.text.element: ['SoNumber']
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
      vbak.gbstk                          as OverallStatus,

      _Items
}
