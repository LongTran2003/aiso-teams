@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Basic View - SO Item'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZI_AISO_SO_ITEM
  as select from vbap
{
 @ObjectModel.text.element: ['SoNumber']
  key vbap.vbeln                              as SoNumber,
  key vbap.posnr                              as ItemNo,
      vbap.matnr                              as Material,
      vbap.werks                              as Plant,
      @Semantics.quantity.unitOfMeasure: 'Unit'
      vbap.kwmeng                             as OrderQty,
      vbap.vrkme                              as Unit,
      @Semantics.amount.currencyCode: 'Currency'
      vbap.netwr                              as NetValue,
      vbap.waerk                              as Currency,
      vbap.abgru                              as RejectionRsn
}
