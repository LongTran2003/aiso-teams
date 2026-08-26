@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Sales Org List'
define view entity ZC_AISO_SALES_ORG_LIST
  as select from ZI_AISO_SALES_ORG_LIST
{
  key SalesOrg,
      SalesOrgName
}
