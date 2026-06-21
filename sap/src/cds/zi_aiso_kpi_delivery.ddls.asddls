@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO KPI - Delivery Status'

define view entity ZI_AISO_KPI_DELIVERY
  as select from likp
{
  key likp.vbeln                                    as DeliveryNumber,
      likp.lfdat                                    as PlannedDelivDate,
      likp.wadat_ist                                as ActualGoodsIssueDate,
      likp.wbstk                                    as GoodsMovementStatus,
      case
        when likp.wadat_ist = '00000000' then 'PENDING'
        when likp.wadat_ist <= likp.lfdat then 'ON_TIME'
        else 'LATE'
      end                                            as DeliveryStatus
}
