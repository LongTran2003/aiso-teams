REPORT zaiso_r_so_detail.

TABLES: vbak, vbap.

PARAMETERS p_vbeln TYPE vbak-vbeln OBLIGATORY.

TYPES: BEGIN OF ty_item,
         posnr TYPE vbap-posnr,
         matnr TYPE vbap-matnr,
         arktx TYPE vbap-arktx,
         werks TYPE vbap-werks,
         kwmeng TYPE vbap-kwmeng,
         vrkme TYPE vbap-vrkme,
         netwr TYPE vbap-netwr,
         waerk TYPE vbak-waerk,
         abgru TYPE vbap-abgru,
       END OF ty_item.

DATA: lv_vbeln TYPE vbak-vbeln,
      lt_items TYPE STANDARD TABLE OF ty_item WITH EMPTY KEY,
      lo_salv  TYPE REF TO cl_salv_table,
      lo_cols  TYPE REF TO cl_salv_columns_table,
      lo_col   TYPE REF TO cl_salv_column_table,
      lo_funcs TYPE REF TO cl_salv_functions_list,
      lx_msg   TYPE REF TO cx_salv_msg.

START-OF-SELECTION.

  lv_vbeln = |{ p_vbeln ALPHA = IN }|.

  SELECT SINGLE vbeln,
                auart,
                kunnr,
                vkorg,
                vtweg,
                spart,
                waerk,
                netwr,
                audat,
                ernam,
                erdat,
                gbstk
    FROM vbak
    WHERE vbeln = @lv_vbeln
    INTO @DATA(ls_header).

  IF sy-subrc <> 0.
    MESSAGE 'Sales order not found.' TYPE 'S' DISPLAY LIKE 'E'.
    RETURN.
  ENDIF.

  SELECT posnr,
         matnr,
         arktx,
         werks,
         kwmeng,
         vrkme,
         netwr,
         @ls_header-waerk AS waerk,
         abgru
    FROM vbap
    WHERE vbeln = @lv_vbeln
    ORDER BY posnr
    INTO TABLE @lt_items.

  WRITE: / 'Sales Order:', ls_header-vbeln,
         / 'Document Type:', ls_header-auart,
         / 'Customer:', ls_header-kunnr,
         / 'Sales Org:', ls_header-vkorg,
         / 'Distribution Channel:', ls_header-vtweg,
         / 'Division:', ls_header-spart,
         / 'Currency:', ls_header-waerk,
         / 'Net Value:', ls_header-netwr,
         / 'Document Date:', ls_header-audat,
         / 'Created By:', ls_header-ernam,
         / 'Created Date:', ls_header-erdat,
         / 'Overall Status:', ls_header-gbstk.

  ULINE.

  IF lt_items IS INITIAL.
    MESSAGE 'Sales order has no item.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_items ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'POSNR' ) ).
      lo_col->set_long_text( 'Item No' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'MATNR' ) ).
      lo_col->set_long_text( 'Material' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'ARKTX' ) ).
      lo_col->set_long_text( 'Description' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'WERKS' ) ).
      lo_col->set_long_text( 'Plant' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'KWMENG' ) ).
      lo_col->set_long_text( 'Order Quantity' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'VRKME' ) ).
      lo_col->set_long_text( 'Sales Unit' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'NETWR' ) ).
      lo_col->set_long_text( 'Net Value' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'WAERK' ) ).
      lo_col->set_long_text( 'Currency' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'ABGRU' ) ).
      lo_col->set_long_text( 'Rejection Reason' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header(
    |AISO - Sales Order { lv_vbeln } Items| ).

  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).

  lo_salv->display( ).
