REPORT zaiso_r_so_owner.

TABLES: zaiso_so_map,
        zaiso_audit.

SELECTION-SCREEN BEGIN OF BLOCK b1 WITH FRAME.
  SELECT-OPTIONS: so_sonum FOR zaiso_so_map-so_number,
                  so_user  FOR zaiso_so_map-teams_user_id.
SELECTION-SCREEN END OF BLOCK b1.

TYPES: BEGIN OF ty_owner,
         so_number     TYPE zaiso_so_map-so_number,
         teams_user_id TYPE zaiso_so_map-teams_user_id,
       END OF ty_owner.

TYPES: BEGIN OF ty_result,
         so_number     TYPE zaiso_so_map-so_number,
         teams_user_id TYPE zaiso_so_map-teams_user_id,
         forwarded_by  TYPE zaiso_audit-sap_user,
         forwarded_at  TYPE zaiso_audit-created_at,
         remarks       TYPE zaiso_audit-remarks,
       END OF ty_result.

DATA: lt_owner TYPE STANDARD TABLE OF ty_owner WITH EMPTY KEY,
      lt_audit TYPE STANDARD TABLE OF zaiso_audit WITH EMPTY KEY,
      lt_result TYPE STANDARD TABLE OF ty_result WITH EMPTY KEY,
      ls_result TYPE ty_result,
      ls_audit TYPE zaiso_audit,
      lo_salv  TYPE REF TO cl_salv_table,
      lo_cols  TYPE REF TO cl_salv_columns_table,
      lo_col   TYPE REF TO cl_salv_column_table,
      lo_funcs TYPE REF TO cl_salv_functions_list,
      lx_msg   TYPE REF TO cx_salv_msg.

START-OF-SELECTION.

  SELECT so_number,
         teams_user_id
    FROM zaiso_so_map
    INTO TABLE @lt_owner
    WHERE so_number     IN @so_sonum
      AND teams_user_id IN @so_user.

  IF lt_owner IS INITIAL.
    MESSAGE 'No matching ownership found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  SELECT *
    FROM zaiso_audit
    INTO TABLE @lt_audit
    WHERE action_type = 'FORWARD_SO'
      AND so_number IN @so_sonum
    ORDER BY created_at DESCENDING.

  LOOP AT lt_owner INTO DATA(ls_owner).
    CLEAR ls_result.

    ls_result-so_number     = ls_owner-so_number.
    ls_result-teams_user_id = ls_owner-teams_user_id.

    READ TABLE lt_audit INTO ls_audit
      WITH KEY so_number = ls_owner-so_number.

    IF sy-subrc = 0.
      ls_result-forwarded_by = ls_audit-sap_user.
      ls_result-forwarded_at = ls_audit-created_at.
      ls_result-remarks      = ls_audit-remarks.
    ENDIF.

    APPEND ls_result TO lt_result.
  ENDLOOP.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_result ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SO_NUMBER' ) ).
      lo_col->set_long_text( 'Sales Order' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'TEAMS_USER_ID' ) ).
      lo_col->set_long_text( 'Current Owner Teams' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'FORWARDED_BY' ) ).
      lo_col->set_long_text( 'Last Forwarded By' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'FORWARDED_AT' ) ).
      lo_col->set_long_text( 'Forwarded At' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'REMARKS' ) ).
      lo_col->set_long_text( 'Forward Remarks' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header( 'AISO - SO Ownership & Forward History' ).
  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).

  lo_salv->display( ).
