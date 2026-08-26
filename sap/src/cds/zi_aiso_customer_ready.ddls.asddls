@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Customer Readiness Check'
define view entity ZI_AISO_CUSTOMER_READY
  as select from knvv

  inner join kna1 on kna1.kunnr = knvv.kunnr

  inner join tvko on tvko.vkorg = knvv.vkorg          

  inner join knb1 on  knb1.kunnr = knvv.kunnr
                   and knb1.bukrs = tvko.bukrs

  left outer join knvp as _ag on  _ag.kunnr = knvv.kunnr
                               and _ag.vkorg = knvv.vkorg
                               and _ag.vtweg = knvv.vtweg
                               and _ag.spart = knvv.spart
                               and _ag.parvw = 'AG'

  
{
  key knvv.kunnr as Customer,
  key knvv.vkorg as SalesOrg,
  key knvv.vtweg as DistChannel,
  key knvv.spart as Division,

      kna1.name1 as CustomerName,

      // Cờ cảnh báo — không chặn nếu thiếu, vì AG/WE có thể tự suy ra
      case when _ag.kunnr is not null then 'X' else '' end as HasExplicitAG
      
}
where knvv.kalks is not initial 
