REPORT zaiso_r_role_testdata.

PARAMETERS p_user TYPE char100 DEFAULT 'DEV-031'.
PARAMETERS p_role TYPE zaiso_de_role DEFAULT 'MANAGER'.
PARAMETERS p_vkorg TYPE vkorg DEFAULT '1000'.

START-OF-SELECTION.

  DATA lv_sap_user TYPE xubname.
  lv_sap_user = p_user.

  DELETE FROM zaiso_user_role
    WHERE sap_user = @lv_sap_user.

   DATA ls_role TYPE zaiso_user_role.

  CLEAR ls_role.
  ls_role-mandt      = sy-mandt.
  ls_role-sap_user   = lv_sap_user.
  ls_role-role       = p_role.
  ls_role-vkorg      = p_vkorg.
  ls_role-valid_from = '20260701'.
  ls_role-valid_to   = '99991231'.

  INSERT zaiso_user_role FROM ls_role.

  IF sy-subrc = 0.
    COMMIT WORK.
    WRITE: / 'Inserted role successfully:',
           / 'SAP_USER:', lv_sap_user,
           / 'ROLE:', p_role,
           / 'VKORG:', p_vkorg.
  ELSE.
    ROLLBACK WORK.
    WRITE: / 'Insert failed. SY-SUBRC:', sy-subrc.
  ENDIF.
