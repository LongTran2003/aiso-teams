REPORT zaiso_r_chan.

TABLES zaiso_chan.

SELECTION-SCREEN BEGIN OF BLOCK b1 WITH FRAME.
  SELECT-OPTIONS: so_chan  FOR zaiso_chan-teams_chan,
                  so_vkorg FOR zaiso_chan-sales_org,
                  so_type  FOR zaiso_chan-sub_type,
                  so_user  FOR zaiso_chan-created_by.
  PARAMETERS p_active TYPE zaiso_chan-is_active.
SELECTION-SCREEN END OF BLOCK b1.

TYPES: BEGIN OF ty_chan,
         chan_id    TYPE zaiso_chan-chan_id,
         teams_chan TYPE zaiso_chan-teams_chan,
         sales_org  TYPE zaiso_chan-sales_org,
         sub_type   TYPE zaiso_chan-sub_type,
         is_active  TYPE zaiso_chan-is_active,
         created_by TYPE zaiso_chan-created_by,
         created_at TYPE zaiso_chan-created_at,
       END OF ty_chan.

DATA: lt_chan  TYPE STANDARD TABLE OF ty_chan WITH EMPTY KEY,
      lo_salv  TYPE REF TO cl_salv_table,
      lo_cols  TYPE REF TO cl_salv_columns_table,
      lo_col   TYPE REF TO cl_salv_column_table,
      lo_funcs TYPE REF TO cl_salv_functions_list,
      lx_msg   TYPE REF TO cx_salv_msg.

START-OF-SELECTION.

  PERFORM get_data.

  IF lt_chan IS INITIAL.
    MESSAGE 'No matching Teams channel subscription found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  PERFORM display_alv.

FORM get_data.

  IF p_active IS INITIAL.
    SELECT chan_id,
           teams_chan,
           sales_org,
           sub_type,
           is_active,
           created_by,
           created_at
      FROM zaiso_chan
      INTO TABLE @lt_chan
      WHERE teams_chan IN @so_chan
        AND sales_org  IN @so_vkorg
        AND sub_type   IN @so_type
        AND created_by IN @so_user
      ORDER BY created_at DESCENDING.
  ELSE.
    SELECT chan_id,
           teams_chan,
           sales_org,
           sub_type,
           is_active,
           created_by,
           created_at
      FROM zaiso_chan
      INTO TABLE @lt_chan
      WHERE teams_chan IN @so_chan
        AND sales_org  IN @so_vkorg
        AND sub_type   IN @so_type
        AND created_by IN @so_user
        AND is_active  = @p_active
      ORDER BY created_at DESCENDING.
  ENDIF.

ENDFORM.

FORM display_alv.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_chan ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CHAN_ID' ) ).
      lo_col->set_visible( abap_false ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'TEAMS_CHAN' ) ).
      lo_col->set_long_text( 'Teams Channel' ).
      lo_col->set_medium_text( 'Teams Channel' ).
      lo_col->set_short_text( 'Channel' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SALES_ORG' ) ).
      lo_col->set_long_text( 'Sales Organization' ).
      lo_col->set_medium_text( 'Sales Org' ).
      lo_col->set_short_text( 'SOrg' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SUB_TYPE' ) ).
      lo_col->set_long_text( 'Subscription Type' ).
      lo_col->set_medium_text( 'Sub Type' ).
      lo_col->set_short_text( 'Type' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'IS_ACTIVE' ) ).
      lo_col->set_long_text( 'Active' ).
      lo_col->set_medium_text( 'Active' ).
      lo_col->set_short_text( 'Act' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CREATED_BY' ) ).
      lo_col->set_long_text( 'Created By' ).
      lo_col->set_medium_text( 'Created By' ).
      lo_col->set_short_text( 'User' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CREATED_AT' ) ).
      lo_col->set_long_text( 'Created At' ).
      lo_col->set_medium_text( 'Created At' ).
      lo_col->set_short_text( 'Time' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header(
    'AISO - Teams Channel Subscription' ).

  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).

  lo_salv->display( ).

ENDFORM.
