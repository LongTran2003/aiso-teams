@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Valid Customer per Sales Area'
define view entity ZC_AISO_VALID_CUSTOMER
  as select from ZI_AISO_VALID_CUSTOMER
{
  key Customer,
  key SalesOrg,
  key DistrChannel,
  key Division,
      CustomerName,
      Country
}
