@AbapCatalog.viewEnhancementCategory: [#NONE]
@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO - SO Reject Status Aggregate'
@Metadata.ignorePropagatedAnnotations: true
define view entity ZI_AISO_SO_REJECT_STATUS as select from vbap
{
  key vbeln                                          as SoNumber,
      count(*)                                       as TotalItems,
      sum( case when abgru <> '' then 1 else 0 end )  as RejectedItems
}
group by vbeln
