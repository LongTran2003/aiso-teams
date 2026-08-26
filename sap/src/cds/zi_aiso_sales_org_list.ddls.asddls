@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Distinct Sales Org'
define view entity ZI_AISO_SALES_ORG_LIST
  as select distinct from knvv
  association [0..1] to tvkot as _SalesOrgText
    on  $projection.SalesOrg = _SalesOrgText.vkorg
    and _SalesOrgText.spras  = $session.system_language
{
  key vkorg as SalesOrg,
      _SalesOrgText.vtext as SalesOrgName,
      _SalesOrgText
}
