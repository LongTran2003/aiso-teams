@AbapCatalog.sqlViewName: 'ZIAISOMATERIAL'
@AbapCatalog.compiler.compareFilter: true
@AbapCatalog.preserveKey: true
@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Material List (Interface)'
define view ZI_AISO_MATERIAL as select from mara
  association [0..1] to makt as _MaterialText
    on _MaterialText.matnr = mara.matnr and _MaterialText.spras = $session.system_language
{
  key mara.matnr          as Material,
      _MaterialText.maktx as MaterialName,
      mara.ersda           as CreatedOn,
      _MaterialText
}   
