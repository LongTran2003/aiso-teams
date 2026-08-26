@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Distinct Dist Channel by Sales Org'
define view entity ZI_AISO_DIST_CHANNEL_LIST
  as select distinct from knvv
{
  key vkorg as SalesOrg,
  key vtweg as DistChannel
}
