@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO User Role Query (Read-only)'
define view entity ZI_AISO_USER_ROLE_QUERY
  as select from zaiso_user_role
{
  key sap_user   as SapUser,
      vkorg      as SalesOrg,
      role       as Role,
      valid_from as ValidFrom,
      valid_to   as ValidTo
}
