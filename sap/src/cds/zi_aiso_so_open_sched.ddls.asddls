@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO - Earliest open schedule date per SO'
define view entity ZI_AISO_SO_OPEN_SCHED
  as select from vbep as schedule
  inner join vbup as status on  status.vbeln = schedule.vbeln
                             and status.posnr = schedule.posnr
{
  key schedule.vbeln         as SoNumber,
      min( schedule.edatu )  as EarliestOpenScheduleDate
}
where
  status.lfsta <> 'C'    -- delivery status chưa hoàn tất
  and status.absta <> 'C' -- chưa bị reject (kiểm tra lại tên field/giá trị đúng ở Bước 4)
group by schedule.vbeln
