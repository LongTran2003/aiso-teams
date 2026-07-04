@EndUserText.label: 'AISO KPI – Revenue'
@AbapCatalog.viewEnhancementCategory: [#NONE]
@AccessControl.authorizationCheck: #NOT_REQUIRED
@Metadata.allowExtensions: true
@ObjectModel.usageType:{
  serviceQuality: #X,
  sizeCategory: #S,
  dataClass: #MIXED
}
define view entity ZI_AISO_KPI_REVENUE
  as select from vbrk
{
  key vkorg                        as SalesOrg,
  key waerk                        as Currency,
  key erdat                        as BillingDate,
      @Semantics.amount.currencyCode: 'Currency'
      sum( netwr )                 as TotalRevenue,
      count( distinct vbeln )      as InvoiceCount
}
group by
  vkorg,
  waerk,
  erdat
