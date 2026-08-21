@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Valid Material per Sales Area'
define view entity ZI_AISO_VALID_MATERIAL_SALES
  as select from mvke
  association [0..1] to mara as _Material on $projection.Material = _Material.matnr
  association [0..1] to makt as _MaterialText
    on  $projection.Material = _MaterialText.matnr
    and _MaterialText.spras  = $session.system_language
{
  key matnr as Material,
  key vkorg as SalesOrg,
  key vtweg as DistrChannel,

      _MaterialText.maktx as MaterialName,
      _Material.mtart     as MaterialType,
      _Material.meins     as BaseUnit,

      _Material,
      _MaterialText
}
