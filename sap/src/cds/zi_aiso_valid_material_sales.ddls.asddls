define view entity ZI_AISO_VALID_MATERIAL_SALES
  as select from mvke
  inner join tvkwz on  tvkwz.vkorg = mvke.vkorg
                    and tvkwz.vtweg = mvke.vtweg
  inner join marc  on  marc.matnr = mvke.matnr
                    and marc.werks = tvkwz.werks
  association [0..1] to mara as _Material on $projection.Material = _Material.matnr
  association [0..1] to ZI_AISO_MATERIAL_TEXT_FB as _MaterialText
    on $projection.Material = _MaterialText.Material
{
  key mvke.matnr as Material,
  key mvke.vkorg as SalesOrg,
  key mvke.vtweg as DistChannel,
  key tvkwz.werks as Plant,

      _MaterialText.MaterialName as MaterialName,
      _Material.mtart            as MaterialType,
      _Material.meins            as BaseUnit,

      _Material,
      _MaterialText
}
