@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Sales Area List'
define view entity ZC_AISO_SALES_AREA
  as select from ZI_AISO_SALES_AREA
{
  key SalesOrg,
  key DistrChannel,
  key Division,
      SalesOrgName,
      DistChannelName,
      DivisionName
}