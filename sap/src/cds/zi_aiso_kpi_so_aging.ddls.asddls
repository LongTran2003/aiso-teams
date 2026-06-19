@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO KPI - SO Aging'

define view entity ZI_AISO_KPI_SO_AGING
  as select from vbak
{
  key vbak.vbeln                                          as SoNumber,
      vbak.vkorg                                          as SalesOrg,
      vbak.kunnr                                          as Customer,
      vbak.erdat                                          as CreatedDate,
      vbak.gbstk                                          as OverallStatus,
      dats_days_between(vbak.erdat, $session.system_date) as AgingDays
}
where
  vbak.gbstk <> 'C'
