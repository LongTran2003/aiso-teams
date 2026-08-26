@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Material Text - Min Rank per Material'
define view entity ZI_AISO_MATNR_TEXT_MINRANK
  as select from ZI_AISO_MATNR_TEXT_RANK
{
  key Material,
      min( SortKey ) as MinSortKey
}
group by Material
