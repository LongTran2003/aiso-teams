@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Composite View - SO with Items'
@Metadata.ignorePropagatedAnnotations: true

define root view entity ZC_AISO_SO_WITH_ITEMS
  as projection on ZI_AISO_SO_HEADER
{
  @ObjectModel.text.element: ['SoNumber']
  key SoNumber,
      DocType,
      Customer,
      SalesOrg,
      DistChannel,
      Division,
      Currency,
      @Semantics.amount.currencyCode: 'Currency'
      NetValue,
      DocDate,
      CreatedBy,
      CreatedDate,
      OverallStatus,

      _Items
}
