@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Valid Material per Plant'
define view entity ZI_AISO_VALID_MATERIAL_PLANT
  as select from marc
  association [0..1] to mara as _Material on $projection.Material = _Material.matnr
{
  key matnr as Material,
  key werks as Plant,
      _Material.mtart as MaterialType,
      _Material.meins as BaseUnit
}
