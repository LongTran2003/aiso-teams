@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Valid Sales Area'
define view entity ZI_AISO_SALES_AREA
  as select from tvta
{
  key vkorg as SalesOrg,
  key vtweg as DistChannel,
  key spart as Division
}
