@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Basic View - SO Header'
@Metadata.ignorePropagatedAnnotations: true
@ObjectModel.usageType: { serviceQuality: #X, sizeCategory: #S, dataClass: #MIXED }

define root view entity ZI_AISO_SO_HEADER
  as select from vbak
  association [0..*] to ZI_AISO_SO_ITEM as _Items
    on $projection.SoNumber = _Items.SoNumber
  association [0..1] to kna1 as _Customer
    on $projection.Customer = _Customer.kunnr
{
  @ObjectModel.foreignKey.association: null
  key vbak.vbeln                          as SoNumber,
      vbak.auart                          as DocType,
      vbak.kunnr                          as Customer,
      _Customer.name1                     as CustomerName,
      vbak.bstnk                          as CustomerReference,
      vbak.vkorg                          as SalesOrg,
      vbak.vtweg                          as DistChannel,
      vbak.spart                          as Division,
      vbak.waerk                          as Currency,
      @Semantics.amount.currencyCode: 'Currency'
      vbak.netwr                          as NetValue,
      vbak.audat                          as DocDate,
      vbak.vdatu                          as RequestedDeliveryDate,
      vbak.ernam                          as CreatedBy,
      vbak.erdat                          as CreatedDate,
      vbak.gbstk                          as OverallStatus,

      cast( '' as abap.char( 1 ) )        as CreditStatus,
      cast( '' as abap.char( 2 ) )        as DeliveryBlock,
      cast( '' as abap.char( 1 ) )        as BillingStatus,
      cast( '' as abap.char( 1 ) )        as IsCancelled,

      _Items
}
