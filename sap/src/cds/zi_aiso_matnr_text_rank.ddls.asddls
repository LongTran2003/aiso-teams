@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Material Text - Ranked by Language Priority'
define view entity ZI_AISO_MATNR_TEXT_RANK
  as select from makt
{
  key matnr    as Material,
  key spras    as Language,
      maktx    as MaterialName,

      concat( case when spras = $session.system_language then '1'
                    when spras = 'E'                       then '2'
                    else '3' end,
              spras ) as SortKey
}
