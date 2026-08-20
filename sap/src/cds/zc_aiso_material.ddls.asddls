@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Material List'
@Search.searchable: true
@UI.headerInfo.typeNamePlural: 'Materials'

define root view entity ZC_AISO_MATERIAL
  as select from ZI_AISO_MATERIAL
{
  key Material,

      @Search.defaultSearchElement: true
      @Search.fuzzinessThreshold: 0.7
      MaterialName,

      CreatedOn
}
