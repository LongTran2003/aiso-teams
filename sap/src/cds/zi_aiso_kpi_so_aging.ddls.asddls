@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO KPI - SO Aging'
define view entity ZI_AISO_KPI_SO_AGING
  as select from vbak as header
  inner join ZI_AISO_SO_OPEN_SCHED as open_sched
    on open_sched.SoNumber = header.vbeln
  left outer join kna1 as customer
    on customer.kunnr = header.kunnr
{
  key header.vbeln                                as SoNumber,
      header.vkorg                                 as SalesOrg,
      header.kunnr                                 as Customer,
      customer.name1                                as CustomerName,
      open_sched.EarliestOpenScheduleDate           as ScheduledDeliveryDate,
      cast(
        dats_days_between( open_sched.EarliestOpenScheduleDate, $session.system_date )
        as abap.int4
      )                                              as DaysPastDue,
      header.netwr                                   as NetValue,
      header.waerk                                    as Currency,
      header.gbstk                                    as OverallStatus
}
where
  header.gbstk <> 'C'
  and open_sched.EarliestOpenScheduleDate < $session.system_date
