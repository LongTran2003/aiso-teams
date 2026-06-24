REPORT zaiso_r_audit.

TABLES zaiso_audit.

SELECTION-SCREEN BEGIN OF BLOCK b1 WITH FRAME TITLE TEXT-001.
  SELECT-OPTIONS: so_sonum FOR zaiso_audit-so_number,
                  so_act   FOR zaiso_audit-action_type,
                  so_user  FOR zaiso_audit-sap_user,
                  so_date  FOR zaiso_audit-created_at.
  PARAMETERS p_stat TYPE zaiso_audit-status.
SELECTION-SCREEN END OF BLOCK b1.

TYPES: BEGIN OF ty_audit,
         audit_id    TYPE zaiso_audit-audit_id,
         so_number   TYPE zaiso_audit-so_number,
         action_type TYPE zaiso_audit-action_type,
         sap_user    TYPE zaiso_audit-sap_user,
         status      TYPE zaiso_audit-status,
         remarks     TYPE zaiso_audit-remarks,
         created_at  TYPE zaiso_audit-created_at,
       END OF ty_audit.

DATA: lt_audit TYPE STANDARD TABLE OF ty_audit WITH EMPTY KEY,
      lo_salv  TYPE REF TO cl_salv_table,
      lo_cols  TYPE REF TO cl_salv_columns_table,
      lo_col   TYPE REF TO cl_salv_column_table,
      lo_funcs TYPE REF TO cl_salv_functions_list,
      lx_msg   TYPE REF TO cx_salv_msg.

START-OF-SELECTION.

  PERFORM get_data.

  IF lt_audit IS INITIAL.
    MESSAGE 'No matching audit log found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  PERFORM display_alv.

FORM get_data.
  IF p_stat IS INITIAL.
    SELECT audit_id,
           so_number,
           action_type,
           sap_user,
           status,
           remarks,
           created_at
      FROM zaiso_audit
      INTO TABLE @lt_audit
      WHERE so_number   IN @so_sonum
        AND action_type IN @so_act
        AND sap_user    IN @so_user
        AND created_at  IN @so_date
      ORDER BY created_at DESCENDING.
  ELSE.
    SELECT audit_id,
           so_number,
           action_type,
           sap_user,
           status,
           remarks,
           created_at
      FROM zaiso_audit
      INTO TABLE @lt_audit
      WHERE so_number   IN @so_sonum
        AND action_type IN @so_act
        AND sap_user    IN @so_user
        AND created_at  IN @so_date
        AND status      = @p_stat
      ORDER BY created_at DESCENDING.
  ENDIF.
ENDFORM.

FORM display_alv.
  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_audit ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'AUDIT_ID' ) ).
      lo_col->set_visible( abap_false ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SO_NUMBER' ) ).
      lo_col->set_long_text( 'Sales Order' ).
      lo_col->set_medium_text( 'Sales Order' ).
      lo_col->set_short_text( 'SO' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'ACTION_TYPE' ) ).
      lo_col->set_long_text( 'Action' ).
      lo_col->set_medium_text( 'Action' ).
      lo_col->set_short_text( 'Action' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SAP_USER' ) ).
      lo_col->set_long_text( 'SAP User' ).
      lo_col->set_medium_text( 'SAP User' ).
      lo_col->set_short_text( 'User' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'STATUS' ) ).
      lo_col->set_long_text( 'Status' ).
      lo_col->set_medium_text( 'Status' ).
      lo_col->set_short_text( 'Status' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'REMARKS' ) ).
      lo_col->set_long_text( 'Remarks' ).
      lo_col->set_medium_text( 'Remarks' ).
      lo_col->set_short_text( 'Remarks' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CREATED_AT' ) ).
      lo_col->set_long_text( 'Timestamp' ).
      lo_col->set_medium_text( 'Timestamp' ).
      lo_col->set_short_text( 'Time' ).
    CATCH cx_salv_not_found.
      " Column customization is optional; keep the ALV usable if a column is absent.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header( 'AISO - Bot Action Audit Log' ).
  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).
  lo_salv->display( ).
ENDFORM.
