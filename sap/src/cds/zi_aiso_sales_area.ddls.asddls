@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Valid Sales Area Combinations'
define view entity ZI_AISO_SALES_AREA
  as select distinct from knvv
  association [0..1] to tvkot as _SalesOrgText
    on  $projection.SalesOrg = _SalesOrgText.vkorg
    and _SalesOrgText.spras  = $session.system_language
  association [0..1] to tvtwt as _DistChannelText
    on  $projection.DistrChannel = _DistChannelText.vtweg
    and _DistChannelText.spras   = $session.system_language
  association [0..1] to tspat as _DivisionText
    on  $projection.Division = _DivisionText.spart
    and _DivisionText.spras  = $session.system_language
{
  key vkorg as SalesOrg,
  key vtweg as DistrChannel,
  key spart as Division,

      _SalesOrgText.vtext as SalesOrgName,
      _DistChannelText.vtext as DistChannelName,
      _DivisionText.vtext as DivisionName,

      _SalesOrgText,
      _DistChannelText,
      _DivisionText
}