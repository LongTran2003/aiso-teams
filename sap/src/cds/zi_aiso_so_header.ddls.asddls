@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Basic View - SO Header'
@Metadata.ignorePropagatedAnnotations: true
@ObjectModel.usageType: { serviceQuality: #X, sizeCategory: #S, dataClass: #MIXED }
define root view entity ZI_AISO_SO_HEADER
  as select from vbak
  composition [0..*] of ZI_AISO_SO_ITEM as _Items
  association [0..1] to kna1 as _Customer
    on $projection.Customer = _Customer.kunnr
  association [0..1] to ZI_AISO_SO_REJECT_STATUS as _RejectStatus
    on $projection.SoNumber = _RejectStatus.SoNumber
  association [0..1] to zaiso_so_map as _Owner
    on $projection.SoNumber = _Owner.so_number
  association [0..1] to ZI_AISO_SO_INVALID_MAT as _InvalidMat
    on $projection.SoNumber = _InvalidMat.SoNumber
{
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
      _Owner.sap_user                     as OwnerSapUser,

      cast( '' as abap.char( 1 ) )        as CreditStatus,
      cast( '' as abap.char( 2 ) )        as DeliveryBlock,
      cast( '' as abap.char( 1 ) )        as BillingStatus,
      cast( '' as abap.char( 100 ) )      as RequestingTeamsUser,
      case
        when _RejectStatus.TotalItems > 0
         and _RejectStatus.TotalItems = _RejectStatus.RejectedItems
        then 'X'
        else ''
      end                                  as IsCancelled,

      case
        when _InvalidMat.InvalidMaterialCount > 0
        then 'X'
        else ''
      end                                  as HasInvalidMaterial,

      _Items,
      _RejectStatus,
      _Owner,
      _InvalidMat
}
