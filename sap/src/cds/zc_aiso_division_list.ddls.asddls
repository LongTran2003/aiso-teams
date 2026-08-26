@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Division List'
define view entity ZC_AISO_DIVISION_LIST
  as select from ZI_AISO_DIVISION_LIST
{
  key SalesOrg,
  key DistChannel,
  key Division
}
