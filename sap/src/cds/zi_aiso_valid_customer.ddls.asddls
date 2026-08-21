@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Valid Customer per Sales Area'
define view entity ZI_AISO_VALID_CUSTOMER
  as select from knvv
  association [0..1] to kna1 as _Customer on $projection.Customer = _Customer.kunnr
{
  key kunnr as Customer,
  key vkorg as SalesOrg,
  key vtweg as DistChannel,
  key spart as Division,

      _Customer.name1  as CustomerName,
      _Customer.land1  as Country,

      _Customer
}
