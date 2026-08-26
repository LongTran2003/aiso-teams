@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Dist Channel List'
define view entity ZC_AISO_DIST_CHANNEL_LIST
  as select from ZI_AISO_DIST_CHANNEL_LIST
{
  key SalesOrg,
  key DistChannel
}
