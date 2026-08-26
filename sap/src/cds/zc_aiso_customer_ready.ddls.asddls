@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Customer Readiness Check'
define view entity ZC_AISO_CUSTOMER_READY
  as select from ZI_AISO_CUSTOMER_READY
{
  key Customer,
  key SalesOrg,
  key DistChannel,
  key Division,
      CustomerName
}
