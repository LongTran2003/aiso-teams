REPORT zaiso_r_so_list.

TABLES vbak.

SELECTION-SCREEN BEGIN OF BLOCK b1 WITH FRAME.
  SELECT-OPTIONS: so_vkorg FOR vbak-vkorg,
                  so_audat FOR vbak-audat,
                  so_kunnr FOR vbak-kunnr,
                  so_gbstk FOR vbak-gbstk.
SELECTION-SCREEN END OF BLOCK b1.

TYPES: BEGIN OF ty_so_list,
         vbeln TYPE vbak-vbeln,
         auart TYPE vbak-auart,
         kunnr TYPE vbak-kunnr,
         vkorg TYPE vbak-vkorg,
         waerk TYPE vbak-waerk,
         netwr TYPE vbak-netwr,
         gbstk TYPE vbak-gbstk,
         audat TYPE vbak-audat,
         ernam TYPE vbak-ernam,
       END OF ty_so_list.

DATA: lt_so    TYPE STANDARD TABLE OF ty_so_list WITH EMPTY KEY,
      lo_salv  TYPE REF TO cl_salv_table,
      lo_cols  TYPE REF TO cl_salv_columns_table,
      lo_col   TYPE REF TO cl_salv_column_table,
      lo_funcs TYPE REF TO cl_salv_functions_list,
      lx_msg   TYPE REF TO cx_salv_msg.

START-OF-SELECTION.

  SELECT vbeln,
         auart,
         kunnr,
         vkorg,
         waerk,
         netwr,
         gbstk,
         audat,
         ernam
    FROM vbak
    INTO TABLE @lt_so
    WHERE vkorg IN @so_vkorg
      AND audat IN @so_audat
      AND kunnr IN @so_kunnr
      AND gbstk IN @so_gbstk
    ORDER BY audat DESCENDING.

  IF lt_so IS INITIAL.
    MESSAGE 'No matching sales order found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_so ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'VBELN' ) ).
      lo_col->set_long_text( 'Sales Order' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'AUART' ) ).
      lo_col->set_long_text( 'Document Type' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'KUNNR' ) ).
      lo_col->set_long_text( 'Customer' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'VKORG' ) ).
      lo_col->set_long_text( 'Sales Org' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'WAERK' ) ).
      lo_col->set_long_text( 'Currency' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'NETWR' ) ).
      lo_col->set_long_text( 'Net Value' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'GBSTK' ) ).
      lo_col->set_long_text( 'Overall Status' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'AUDAT' ) ).
      lo_col->set_long_text( 'Document Date' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'ERNAM' ) ).
      lo_col->set_long_text( 'Created By' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header( 'AISO - Sales Order Overview' ).
  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).
  lo_salv->display( ).
