@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Valid Material per Sales Area'
define view entity ZC_AISO_VALID_MATERIAL_SALES
  as select from ZI_AISO_VALID_MATERIAL_SALES
{
  key Material,
  key SalesOrg,
  key DistrChannel,
      MaterialName,
      MaterialType,
      BaseUnit
}
