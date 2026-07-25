REPORT zaiso_r_kpi_dash.

TABLES: vbak, vbrk, likp.

SELECTION-SCREEN BEGIN OF BLOCK b1 WITH FRAME.
  PARAMETERS p_kpi TYPE c LENGTH 1 DEFAULT '1' OBLIGATORY.
  SELECT-OPTIONS so_vkorg FOR vbak-vkorg.
  SELECT-OPTIONS so_date  FOR sy-datum.
  PARAMETERS p_top TYPE i DEFAULT 200.
SELECTION-SCREEN END OF BLOCK b1.

TYPES: BEGIN OF ty_revenue,
         salesorg     TYPE vbrk-vkorg,
         currency     TYPE vbrk-waerk,
         billingdate  TYPE vbrk-erdat,
         totalrevenue TYPE vbrk-netwr,
         invoicecount TYPE i,
       END OF ty_revenue.

TYPES: BEGIN OF ty_delivery,
         deliverynumber       TYPE likp-vbeln,
         planneddelivdate     TYPE likp-lfdat,
         actualgoodsissuedate TYPE likp-wadat_ist,
         goodsmovementstatus  TYPE likp-wbstk,
         deliverystatus       TYPE c LENGTH 20,
       END OF ty_delivery.

TYPES: BEGIN OF ty_so_aging,
         sonumber              TYPE vbak-vbeln,
         salesorg              TYPE vbak-vkorg,
         customer              TYPE vbak-kunnr,
         scheduleddeliverydate TYPE sy-datum,
         overallstatus         TYPE vbak-gbstk,
         dayspastdue           TYPE i,
       END OF ty_so_aging.

TYPES: BEGIN OF ty_ar_aging,
         billingdocnumber TYPE vbrk-vbeln,
         customer         TYPE vbrk-kunrg,
         currency         TYPE vbrk-waerk,
         netvalue         TYPE vbrk-netwr,
         billingdate      TYPE vbrk-fkdat,
         overduedays      TYPE i,
       END OF ty_ar_aging.

DATA: lt_revenue  TYPE STANDARD TABLE OF ty_revenue WITH EMPTY KEY,
      lt_delivery TYPE STANDARD TABLE OF ty_delivery WITH EMPTY KEY,
      lt_so_aging TYPE STANDARD TABLE OF ty_so_aging WITH EMPTY KEY,
      lt_ar_aging TYPE STANDARD TABLE OF ty_ar_aging WITH EMPTY KEY.

START-OF-SELECTION.

  IF p_top IS INITIAL OR p_top < 1.
    p_top = 200.
  ENDIF.

  CASE p_kpi.
    WHEN '1'.
      PERFORM get_revenue.
      PERFORM display_revenue.
    WHEN '2'.
      PERFORM get_delivery.
      PERFORM display_delivery.
    WHEN '3'.
      PERFORM get_so_aging.
      PERFORM display_so_aging.
    WHEN '4'.
      PERFORM get_ar_aging.
      PERFORM display_ar_aging.
    WHEN OTHERS.
      MESSAGE 'Invalid KPI. Use 1=Revenue, 2=Delivery, 3=SO Aging, 4=AR Aging.' TYPE 'S' DISPLAY LIKE 'E'.
  ENDCASE.

FORM get_revenue.
  SELECT salesorg,
         currency,
         billingdate,
         totalrevenue,
         invoicecount
    FROM zi_aiso_kpi_revenue
    WHERE salesorg    IN @so_vkorg
      AND billingdate IN @so_date
    ORDER BY billingdate DESCENDING
    INTO TABLE @lt_revenue
    UP TO @p_top ROWS.
ENDFORM.

FORM get_delivery.
  SELECT deliverynumber,
         planneddelivdate,
         actualgoodsissuedate,
         goodsmovementstatus,
         deliverystatus
    FROM zi_aiso_kpi_delivery
    WHERE planneddelivdate IN @so_date
    ORDER BY planneddelivdate DESCENDING
    INTO TABLE @lt_delivery
    UP TO @p_top ROWS.
ENDFORM.

FORM get_so_aging.
  SELECT sonumber,
         salesorg,
         customer,
         scheduleddeliverydate,
         overallstatus,
         dayspastdue
    FROM zi_aiso_kpi_so_aging
    WHERE salesorg              IN @so_vkorg
      AND scheduleddeliverydate IN @so_date
    ORDER BY dayspastdue DESCENDING
    INTO TABLE @lt_so_aging
    UP TO @p_top ROWS.
ENDFORM.

FORM get_ar_aging.
  SELECT billingdocnumber,
         customer,
         currency,
         netvalue,
         billingdate,
         overduedays
    FROM zi_aiso_kpi_ar_aging
    WHERE billingdate IN @so_date
    ORDER BY overduedays DESCENDING
    INTO TABLE @lt_ar_aging
    UP TO @p_top ROWS.
ENDFORM.

FORM display_revenue.

  DATA: lo_salv  TYPE REF TO cl_salv_table,
        lo_cols  TYPE REF TO cl_salv_columns_table,
        lo_col   TYPE REF TO cl_salv_column_table,
        lo_funcs TYPE REF TO cl_salv_functions_list,
        lx_msg   TYPE REF TO cx_salv_msg.

  IF lt_revenue IS INITIAL.
    MESSAGE 'No revenue KPI data found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_revenue ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SALESORG' ) ).
      lo_col->set_long_text( 'Sales Org' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CURRENCY' ) ).
      lo_col->set_long_text( 'Currency' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'BILLINGDATE' ) ).
      lo_col->set_long_text( 'Billing Date' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'TOTALREVENUE' ) ).
      lo_col->set_long_text( 'Total Revenue' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'INVOICECOUNT' ) ).
      lo_col->set_long_text( 'Invoice Count' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header( 'AISO KPI - Revenue' ).
  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).

  lo_salv->display( ).

ENDFORM.

FORM display_delivery.

  DATA: lo_salv  TYPE REF TO cl_salv_table,
        lo_cols  TYPE REF TO cl_salv_columns_table,
        lo_col   TYPE REF TO cl_salv_column_table,
        lo_funcs TYPE REF TO cl_salv_functions_list,
        lx_msg   TYPE REF TO cx_salv_msg.

  IF lt_delivery IS INITIAL.
    MESSAGE 'No delivery KPI data found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_delivery ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'DELIVERYNUMBER' ) ).
      lo_col->set_long_text( 'Delivery Number' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'PLANNEDDELIVDATE' ) ).
      lo_col->set_long_text( 'Planned Delivery Date' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'ACTUALGOODSISSUEDATE' ) ).
      lo_col->set_long_text( 'Actual GI Date' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'GOODSMOVEMENTSTATUS' ) ).
      lo_col->set_long_text( 'Goods Movement Status' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'DELIVERYSTATUS' ) ).
      lo_col->set_long_text( 'Delivery Status' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header( 'AISO KPI - Delivery Status' ).
  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).

  lo_salv->display( ).

ENDFORM.

FORM display_so_aging.

  DATA: lo_salv  TYPE REF TO cl_salv_table,
        lo_cols  TYPE REF TO cl_salv_columns_table,
        lo_col   TYPE REF TO cl_salv_column_table,
        lo_funcs TYPE REF TO cl_salv_functions_list,
        lx_msg   TYPE REF TO cx_salv_msg.

  IF lt_so_aging IS INITIAL.
    MESSAGE 'No sales order aging KPI data found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_so_aging ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SONUMBER' ) ).
      lo_col->set_long_text( 'Sales Order' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SALESORG' ) ).
      lo_col->set_long_text( 'Sales Org' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CUSTOMER' ) ).
      lo_col->set_long_text( 'Customer' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'SCHEDULEDDELIVERYDATE' ) ).
      lo_col->set_long_text( 'Scheduled Delivery Date' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'OVERALLSTATUS' ) ).
      lo_col->set_long_text( 'Overall Status' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'DAYSPASTDUE' ) ).
      lo_col->set_long_text( 'Days Past Due' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header( 'AISO KPI - Sales Order Aging' ).
  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).

  lo_salv->display( ).

ENDFORM.

FORM display_ar_aging.

  DATA: lo_salv  TYPE REF TO cl_salv_table,
        lo_cols  TYPE REF TO cl_salv_columns_table,
        lo_col   TYPE REF TO cl_salv_column_table,
        lo_funcs TYPE REF TO cl_salv_functions_list,
        lx_msg   TYPE REF TO cx_salv_msg.

  IF lt_ar_aging IS INITIAL.
    MESSAGE 'No AR aging KPI data found.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  TRY.
      cl_salv_table=>factory(
        IMPORTING
          r_salv_table = lo_salv
        CHANGING
          t_table      = lt_ar_aging ).
    CATCH cx_salv_msg INTO lx_msg.
      MESSAGE lx_msg->get_text( ) TYPE 'E'.
      RETURN.
  ENDTRY.

  lo_funcs = lo_salv->get_functions( ).
  lo_funcs->set_all( abap_true ).

  lo_cols = lo_salv->get_columns( ).
  lo_cols->set_optimize( abap_true ).

  TRY.
      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'BILLINGDOCNUMBER' ) ).
      lo_col->set_long_text( 'Billing Document' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CUSTOMER' ) ).
      lo_col->set_long_text( 'Customer' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'CURRENCY' ) ).
      lo_col->set_long_text( 'Currency' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'NETVALUE' ) ).
      lo_col->set_long_text( 'Net Value' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'BILLINGDATE' ) ).
      lo_col->set_long_text( 'Billing Date' ).

      lo_col = CAST cl_salv_column_table( lo_cols->get_column( 'OVERDUEDAYS' ) ).
      lo_col->set_long_text( 'Overdue Days' ).
    CATCH cx_salv_not_found.
  ENDTRY.

  lo_salv->get_display_settings( )->set_list_header( 'AISO KPI - AR Aging' ).
  lo_salv->get_display_settings( )->set_striped_pattern( abap_true ).

  lo_salv->display( ).

ENDFORM.
