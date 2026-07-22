@AbapCatalog.sqlViewName: 'ZIAISOAUDIT'
@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Audit Log'
@AbapCatalog.preserveKey: true
define root view ZI_AISO_AUDIT
  as select from zaiso_audit
{
  key audit_id    as AuditId,
      so_number   as SoNumber,
      action_type as ActionType,
      status      as Status,
      remarks     as Remarks,
      sap_user    as SapUser,
      created_at  as CreatedAt
}
