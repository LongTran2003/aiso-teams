@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Basic View - SO Item'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZI_AISO_SO_ITEM
  as select from vbap

  association to parent ZI_AISO_SO_HEADER as _Header
    on $projection.SoNumber = _Header.SoNumber

  association [0..1] to makt as _Material
    on  $projection.Material = _Material.matnr
    and _Material.spras      = $session.system_language
{
  key vbap.vbeln                              as SoNumber,
  key vbap.posnr                              as ItemNo,
      vbap.matnr                              as Material,
      _Material.maktx                         as MaterialName,
      vbap.werks                              as Plant,
      @Semantics.quantity.unitOfMeasure: 'Unit'
      vbap.kwmeng                             as OrderQty,
      vbap.vrkme                              as Unit,
      @Semantics.amount.currencyCode: 'Currency'
      vbap.netwr                              as NetValue,
      vbap.waerk                              as Currency,
      vbap.abgru                              as RejectionRsn,

      _Header,
      _Material
}
