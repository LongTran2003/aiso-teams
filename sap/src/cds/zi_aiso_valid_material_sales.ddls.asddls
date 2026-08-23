@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Valid Material per Sales Area (Plant-checked)'
define view entity ZI_AISO_VALID_MATERIAL_SALES
  as select from mvke
  inner join tvkwz on  tvkwz.vkorg = mvke.vkorg
                    and tvkwz.vtweg = mvke.vtweg
  inner join marc  on  marc.matnr = mvke.matnr
                    and marc.werks = tvkwz.werks
  association [0..1] to mara as _Material on $projection.Material = _Material.matnr
  association [0..1] to makt as _MaterialText
    on  $projection.Material = _MaterialText.matnr
    and _MaterialText.spras  = $session.system_language
{
  key mvke.matnr as Material,
  key mvke.vkorg as SalesOrg,
  key mvke.vtweg as DistChannel,
  key tvkwz.werks as Plant,

      _MaterialText.maktx as MaterialName,
      _Material.mtart     as MaterialType,
      _Material.meins     as BaseUnit,

      _Material,
      _MaterialText
}
