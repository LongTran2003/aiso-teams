@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO User Role Basic View'
define view entity ZI_AISO_USER_ROLE
  as select from zaiso_user_role
{
  key sap_user   as SapUser,
      role       as Role,
      vkorg      as SalesOrg,
      valid_from as ValidFrom,
      valid_to   as ValidTo
}
