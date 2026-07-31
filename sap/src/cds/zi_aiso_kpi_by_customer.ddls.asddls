@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO KPI - By Customer'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZI_AISO_KPI_BY_CUSTOMER
  as select from vbak
  association [0..1] to kna1 as _Customer
    on $projection.Customer = _Customer.kunnr
{
  key vbak.kunnr                                     as Customer,
      _Customer.name1                                as CustomerName,
      vbak.vkorg                                      as SalesOrg,
      vbak.waerk                                      as Currency,

      @Semantics.amount.currencyCode: 'Currency'
      cast( sum( vbak.netwr ) as abap.curr( 15, 2 ) )  as TotalRevenue,

      count( distinct vbak.vbeln )                    as OrderCount,

      cast(
        div( cast( count( distinct case when vbak.gbstk = 'C' then vbak.vbeln end ) as abap.dec( 15, 2 ) ) * 100,
             cast( count( distinct vbak.vbeln ) as abap.dec( 15, 2 ) ) )
        as abap.dec( 5, 1 )
      )                                                as FulfillmentRate
}
group by vbak.kunnr, _Customer.name1, vbak.vkorg, vbak.waerk
