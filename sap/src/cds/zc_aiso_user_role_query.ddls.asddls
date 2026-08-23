@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO User Role Query'
define view entity ZC_AISO_USER_ROLE_QUERY
  as select from ZI_AISO_USER_ROLE_QUERY
{
  key SapUser,
      SalesOrg,
      Role,
      ValidFrom,
      ValidTo
}
