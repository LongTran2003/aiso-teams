@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Composition View - SO Item'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZC_AISO_SO_ITEM
  as projection on ZI_AISO_SO_ITEM
{
  key SoNumber,
  key ItemNo,
      Material,
      MaterialName,
      Plant,
      @Semantics.quantity.unitOfMeasure: 'Unit'
      OrderQty,
      Unit,
      @Semantics.amount.currencyCode: 'Currency'
      NetValue,
      Currency,
      RejectionRsn,
      _Header : redirected to parent ZC_AISO_SO_WITH_ITEMS
}
