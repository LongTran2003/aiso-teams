@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO KPI - AR Aging'

define view entity ZI_AISO_KPI_AR_AGING
  as select from vbrk
{
  key vbrk.vbeln                                          as BillingDocNumber,
      vbrk.kunrg                                          as Customer,
      vbrk.waerk                                          as Currency,
      @Semantics.amount.currencyCode: 'Currency'
      vbrk.netwr                                          as NetValue,
      vbrk.fkdat                                          as BillingDate,
      dats_days_between(vbrk.fkdat, $session.system_date) as OverdueDays
}
where
  vbrk.fksto <> 'X'
