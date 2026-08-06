
@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO User Role - Interface'
define root view entity ZI_AISO_USER_ROLE
  as select from zaiso_user_role
{
  key sap_user   as SapUser,
      role       as UserRole,
      vkorg      as SalesOrg,
      valid_from as ValidFrom,
      valid_to   as ValidTo
}
