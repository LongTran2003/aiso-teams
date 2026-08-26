@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO Doc Type List'
define view entity ZC_AISO_DOC_TYPE_LIST
  as select from ZI_AISO_DOC_TYPE_LIST
{
  key DocType,
      DocTypeName
}
