@EndUserText.label: 'Parameters for User Role Sync Action'
define abstract entity ZI_AISO_SYNC_ROLE_PARAMS
{
  sap_user              : abap.char(12);
  new_role              : abap.char(20);
  sales_org             : abap.char(4);
  requesting_teams_user : abap.char(100);
}
