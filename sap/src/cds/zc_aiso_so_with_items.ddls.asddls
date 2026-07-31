@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Composite View - SO with Items'
@Metadata.ignorePropagatedAnnotations: true

define root view entity ZC_AISO_SO_WITH_ITEMS
  as projection on ZI_AISO_SO_HEADER
{
  key SoNumber,
      DocType,
      Customer,
      CustomerName,
      CustomerReference,
      SalesOrg,
      DistChannel,
      Division,
      Currency,
      @Semantics.amount.currencyCode: 'Currency'
      NetValue,
      DocDate,
      RequestedDeliveryDate,
      CreatedBy,
      CreatedDate,
      OverallStatus,
      IsCancelled,
      HasInvalidMaterial,
      OwnerSapUser,
      
      _Items
}
