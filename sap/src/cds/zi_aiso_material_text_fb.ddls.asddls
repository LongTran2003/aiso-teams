@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Material Text - Fallback Resolved'
define view entity ZI_AISO_MATERIAL_TEXT_FB
  as select from ZI_AISO_MATNR_TEXT_RANK as rank
  inner join ZI_AISO_MATNR_TEXT_MINRANK as minrank
    on  rank.Material = minrank.Material
    and rank.SortKey  = minrank.MinSortKey
{
  key rank.Material,
      rank.MaterialName
}
