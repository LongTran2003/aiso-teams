@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Valid Material Check'
define view entity ZI_AISO_VALID_MATERIAL
  as select from mara
{
  key matnr as Material,
      mtart as MaterialType,
      matkl as MaterialGroup,
      meins as BaseUnit
}
