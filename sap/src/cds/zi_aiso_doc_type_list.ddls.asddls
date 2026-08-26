@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Doc Type List'
define view entity ZI_AISO_DOC_TYPE_LIST
  as select from tvak
  association [0..1] to tvakt as _Text
    on  $projection.DocType = _Text.auart
    and _Text.spras         = $session.system_language
{
  key auart as DocType,
      _Text.bezei as DocTypeName,
      _Text
}
