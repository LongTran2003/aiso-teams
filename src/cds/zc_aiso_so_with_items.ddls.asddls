@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Composite View - SO with Items'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZC_AISO_SO_WITH_ITEMS
  as select from ZI_AISO_SO_HEADER
  association [0..*] to ZI_AISO_SO_ITEM as _Items
    on $projection.SoNumber = _Items.SoNumber
{
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
