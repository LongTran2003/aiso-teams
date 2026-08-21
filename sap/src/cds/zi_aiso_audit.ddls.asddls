@AccessControl.authorizationCheck: #NOT_REQUIRED
@EndUserText.label: 'AISO Audit Log'
define root view entity ZI_AISO_AUDIT
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
