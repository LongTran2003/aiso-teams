CLASS lhc_SalesOrder DEFINITION INHERITING FROM cl_abap_behavior_handler.

  PRIVATE SECTION.

    METHODS createSalesOrder FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~createSalesOrder RESULT result.

    METHODS cancelOrder FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~cancelOrder RESULT result.

    METHODS updateReference FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~updateReference RESULT result.

    METHODS read FOR READ
      IMPORTING keys FOR READ SalesOrder RESULT result.

    METHODS lock FOR LOCK
      IMPORTING keys FOR LOCK SalesOrder.

    METHODS releaseOrder FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~releaseOrder RESULT result.

    METHODS forwardOrder FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~forwardOrder RESULT result.
    METHODS rejectOrder FOR MODIFY
  IMPORTING keys FOR ACTION SalesOrder~rejectOrder RESULT result.

ENDCLASS.

CLASS lhc_SalesOrder IMPLEMENTATION.

  METHOD createSalesOrder.
    DATA: ls_header_in  TYPE bapisdhead1,
          ls_header_inx TYPE bapisdhead1x,
          lt_partners   TYPE TABLE OF bapiparnr,
          lt_items_in   TYPE TABLE OF bapiitemin,
          lt_return     TYPE TABLE OF bapiret2,
          lv_so_number  TYPE vbeln_va,
          lv_timestamp  TYPE c LENGTH 14.

    LOOP AT keys INTO DATA(ls_key).

      CLEAR: ls_header_in, ls_header_inx, lt_partners, lt_items_in, lt_return, lv_so_number.

      ls_header_in-doc_type   = ls_key-%param-doc_type.
      ls_header_in-sales_org  = ls_key-%param-sales_org.
      ls_header_in-distr_chan = ls_key-%param-dist_channel.
      ls_header_in-division   = ls_key-%param-division.
      ls_header_in-currency   = ls_key-%param-currency.

      ls_header_inx-doc_type   = 'X'.
      ls_header_inx-sales_org  = 'X'.
      ls_header_inx-distr_chan = 'X'.
      ls_header_inx-division   = 'X'.
      ls_header_inx-currency   = 'X'.

      APPEND VALUE #( partn_role = 'AG' partn_numb = ls_key-%param-customer ) TO lt_partners.

      LOOP AT ls_key-%param-items INTO DATA(ls_item).
        APPEND VALUE #( material   = ls_item-material
                         plant      = ls_item-plant
                         target_qty = ls_item-order_qty
                         target_qu  = ls_item-unit ) TO lt_items_in.
      ENDLOOP.

      CALL FUNCTION 'BAPI_SALESORDER_CREATEFROMDAT2'
        EXPORTING
          order_header_in  = ls_header_in
          order_header_inx = ls_header_inx
        IMPORTING
          salesdocument    = lv_so_number
        TABLES
          return           = lt_return
          order_partners   = lt_partners
          order_items_in   = lt_items_in.

      READ TABLE lt_return WITH KEY type = 'E' TRANSPORTING NO FIELDS.
      IF sy-subrc = 0.
        CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
        APPEND VALUE #( %cid = ls_key-%cid %fail-cause = if_abap_behv=>cause-unspecific ) TO failed-salesorder.
        CONTINUE.
      ENDIF.

      CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING wait = 'X'.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      INSERT zaiso_audit FROM @( VALUE #(
        mandt       = sy-mandt
        audit_id    = cl_system_uuid=>create_uuid_c32_static( )
        sap_user    = sy-uname
        action_type = 'CREATE_SO'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        created_at  = lv_timestamp
      ) ).

     DATA ls_result LIKE LINE OF result.
      CLEAR ls_result.
      ls_result-%cid = ls_key-%cid.
      ls_result-%param-SoNumber = lv_so_number.
      APPEND ls_result TO result.
    ENDLOOP.
  ENDMETHOD.

  METHOD cancelorder.
  DATA: lt_items_in  TYPE TABLE OF bapisditm,
        lt_items_inx TYPE TABLE OF bapisditmx,
        lt_return    TYPE TABLE OF bapiret2,
        lv_timestamp TYPE c LENGTH 14.

  LOOP AT keys INTO DATA(ls_key).

    " Fix ALPHA conversion
    DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

    SELECT SINGLE teams_user_id FROM zaiso_so_map
      INTO @DATA(lv_owner)
      WHERE so_number = @lv_so_number.

    IF lv_owner IS NOT INITIAL AND lv_owner <> ls_key-%param-requesting_teams_user.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-salesorder.
      CONTINUE.
    ENDIF.

    CLEAR: lt_items_in, lt_items_inx, lt_return.

    SELECT posnr FROM vbap
      INTO TABLE @DATA(lt_posnr)
      WHERE vbeln = @lv_so_number.

    LOOP AT lt_posnr INTO DATA(ls_posnr).
      APPEND VALUE #( itm_number = ls_posnr-posnr
                       reason_rej = 'Z1' ) TO lt_items_in.
      APPEND VALUE #( itm_number = ls_posnr-posnr
                       reason_rej = 'X' )  TO lt_items_inx.
    ENDLOOP.

    CALL FUNCTION 'BAPI_SALESORDER_CHANGE'
      EXPORTING
        salesdocument  = lv_so_number
      TABLES
        order_item_in  = lt_items_in
        order_item_inx = lt_items_inx
        return         = lt_return.

    READ TABLE lt_return WITH KEY type = 'E' TRANSPORTING NO FIELDS.
    IF sy-subrc = 0.
      CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-salesorder.
      CONTINUE.
    ENDIF.

    CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING wait = 'X'.

    CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

    INSERT zaiso_audit FROM @( VALUE #(
      mandt       = sy-mandt
      audit_id    = cl_system_uuid=>create_uuid_c32_static( )
      sap_user    = sy-uname
      action_type = 'CANCEL_SO'
      so_number   = lv_so_number
      status      = 'SUCCESS'
      created_at  = lv_timestamp
    ) ).

    APPEND VALUE #( %tky = ls_key-%tky ) TO result.
  ENDLOOP.
ENDMETHOD.


METHOD updatereference.
  DATA: ls_header_in  TYPE bapisdh1,
        ls_header_inx TYPE bapisdh1x,
        lt_return     TYPE TABLE OF bapiret2,
        lv_timestamp  TYPE c LENGTH 14.

  LOOP AT keys INTO DATA(ls_key).

    " Fix ALPHA conversion
    DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

    SELECT SINGLE teams_user_id FROM zaiso_so_map
      INTO @DATA(lv_owner)
      WHERE so_number = @lv_so_number.

    IF lv_owner IS NOT INITIAL AND lv_owner <> ls_key-%param-requesting_teams_user.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-salesorder.
      CONTINUE.
    ENDIF.

    CLEAR: ls_header_in, ls_header_inx, lt_return.
    ls_header_in-purch_no_c  = ls_key-%param-new_reference.
    ls_header_inx-purch_no_c = 'X'.

    CALL FUNCTION 'BAPI_SALESORDER_CHANGE'
      EXPORTING
        salesdocument    = lv_so_number
        order_header_in  = ls_header_in
        order_header_inx = ls_header_inx
      TABLES
        return           = lt_return.

    READ TABLE lt_return WITH KEY type = 'E' TRANSPORTING NO FIELDS.
    IF sy-subrc = 0.
      CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-salesorder.
      CONTINUE.
    ENDIF.

    CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING wait = 'X'.

    CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

    INSERT zaiso_audit FROM @( VALUE #(
      mandt       = sy-mandt
      audit_id    = cl_system_uuid=>create_uuid_c32_static( )
      sap_user    = sy-uname
      action_type = 'UPDATE_REF_SO'
      so_number   = lv_so_number
      status      = 'SUCCESS'
      created_at  = lv_timestamp
    ) ).

    APPEND VALUE #( %tky = ls_key-%tky ) TO result.
  ENDLOOP.
ENDMETHOD.
METHOD read.
    DATA(lt_keys) = keys.
    SELECT * FROM vbak
      FOR ALL ENTRIES IN @lt_keys
      WHERE vbeln = @lt_keys-SoNumber
      INTO TABLE @DATA(lt_vbak).

    LOOP AT lt_vbak INTO DATA(ls_vbak).
      APPEND VALUE #(
        %key-SoNumber = ls_vbak-vbeln
        SoNumber      = ls_vbak-vbeln
        DocType       = ls_vbak-auart
        Customer      = ls_vbak-kunnr
        SalesOrg      = ls_vbak-vkorg
        DistChannel   = ls_vbak-vtweg
        Division      = ls_vbak-spart
        Currency      = ls_vbak-waerk
        NetValue      = ls_vbak-netwr
        DocDate       = ls_vbak-audat
        CreatedBy     = ls_vbak-ernam
        CreatedDate   = ls_vbak-erdat
        OverallStatus = ls_vbak-gbstk
      ) TO result.
    ENDLOOP.
  ENDMETHOD.
  METHOD lock.
    " BAPI tự enqueue VBAK nội bộ, không cần custom lock logic ở đây
  ENDMETHOD.

METHOD releaseorder.
  DATA: ls_header_in  TYPE bapisdh1,
        ls_header_inx TYPE bapisdh1x,
        lt_return     TYPE TABLE OF bapiret2,
        lv_timestamp  TYPE c LENGTH 14.

  LOOP AT keys INTO DATA(ls_key).

    " Fix ALPHA conversion
    DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

    SELECT SINGLE teams_user_id FROM zaiso_so_map
      INTO @DATA(lv_owner)
      WHERE so_number = @lv_so_number.

    IF lv_owner IS NOT INITIAL AND lv_owner <> ls_key-%param-requesting_teams_user.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-salesorder.
      CONTINUE.
    ENDIF.

    CLEAR: ls_header_in, ls_header_inx, lt_return.
    ls_header_in-dlv_block  = ''.
    ls_header_inx-dlv_block = 'X'.

    CALL FUNCTION 'BAPI_SALESORDER_CHANGE'
      EXPORTING
        salesdocument    = lv_so_number
        order_header_in  = ls_header_in
        order_header_inx = ls_header_inx
      TABLES
        return           = lt_return.

    READ TABLE lt_return WITH KEY type = 'E' TRANSPORTING NO FIELDS.
    IF sy-subrc = 0.
      CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-salesorder.
      CONTINUE.
    ENDIF.

    CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING wait = 'X'.

    CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

    INSERT zaiso_audit FROM @( VALUE #(
      mandt       = sy-mandt
      audit_id    = cl_system_uuid=>create_uuid_c32_static( )
      sap_user    = sy-uname
      action_type = 'RELEASE_SO'
      so_number   = lv_so_number
      status      = 'SUCCESS'
      created_at  = lv_timestamp
    ) ).

    APPEND VALUE #( %tky = ls_key-%tky ) TO result.
  ENDLOOP.
ENDMETHOD.


METHOD forwardorder.
  DATA: lv_timestamp TYPE c LENGTH 14.

  LOOP AT keys INTO DATA(ls_key).
    DATA(ls_param) = ls_key-%param.

    " Fix ALPHA conversion
    DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

    SELECT SINGLE teams_user_id FROM zaiso_so_map
      INTO @DATA(lv_owner)
      WHERE so_number = @lv_so_number.

    IF lv_owner IS NOT INITIAL AND lv_owner <> ls_param-requesting_teams_user.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-SalesOrder.
      CONTINUE.
    ENDIF.

    UPDATE zaiso_so_map
      SET teams_user_id = @ls_param-new_teams_user
      WHERE so_number   = @lv_so_number.

    IF sy-subrc <> 0.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-SalesOrder.
      CONTINUE.
    ENDIF.

    CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

    INSERT zaiso_audit FROM @( VALUE #(
      mandt       = sy-mandt
      audit_id    = cl_system_uuid=>create_uuid_c32_static( )
      sap_user    = sy-uname
      action_type = 'FORWARD_SO'
      so_number   = lv_so_number
      status      = 'SUCCESS'
      remarks     = ls_param-remarks
      created_at  = lv_timestamp
    ) ).

    APPEND VALUE #( %tky = ls_key-%tky ) TO result.
  ENDLOOP.
ENDMETHOD.


METHOD rejectorder.
  DATA: lt_items_in  TYPE TABLE OF bapisditm,
        lt_items_inx TYPE TABLE OF bapisditmx,
        lt_return    TYPE TABLE OF bapiret2,
        lv_timestamp TYPE c LENGTH 14.

  LOOP AT keys INTO DATA(ls_key).

    " Fix ALPHA conversion
    DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

    " Validate rejection code
    IF ls_key-%param-rejection_code <> '02' AND
       ls_key-%param-rejection_code <> '03' AND
       ls_key-%param-rejection_code <> '04'.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-SalesOrder.
      CONTINUE.
    ENDIF.

    " Check role ZROLE_AISO_BOT_RELEASER
    SELECT SINGLE uname FROM agr_users
      INTO @DATA(lv_auth_check)
      WHERE agr_name = 'ZROLE_AISO_BOT_RELEASER'
        AND uname    = @sy-uname.

    IF sy-subrc <> 0.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-SalesOrder.
      CONTINUE.
    ENDIF.

    CLEAR: lt_items_in, lt_items_inx, lt_return.

    SELECT posnr FROM vbap
      INTO TABLE @DATA(lt_posnr)
      WHERE vbeln = @lv_so_number.

    IF lt_posnr IS INITIAL.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-not_found )
             TO failed-SalesOrder.
      CONTINUE.
    ENDIF.

    LOOP AT lt_posnr INTO DATA(ls_posnr).
      APPEND VALUE #(
        itm_number = ls_posnr-posnr
        reason_rej = ls_key-%param-rejection_code
      ) TO lt_items_in.
      APPEND VALUE #(
        itm_number = ls_posnr-posnr
        reason_rej = 'X'
      ) TO lt_items_inx.
    ENDLOOP.

    CALL FUNCTION 'BAPI_SALESORDER_CHANGE'
      EXPORTING
        salesdocument  = lv_so_number
      TABLES
        order_item_in  = lt_items_in
        order_item_inx = lt_items_inx
        return         = lt_return.

    READ TABLE lt_return WITH KEY type = 'E' TRANSPORTING NO FIELDS.
    IF sy-subrc = 0.
      CALL FUNCTION 'BAPI_TRANSACTION_ROLLBACK'.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-SalesOrder.
      CONTINUE.
    ENDIF.

    CALL FUNCTION 'BAPI_TRANSACTION_COMMIT' EXPORTING wait = 'X'.

    CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

    INSERT zaiso_audit FROM @( VALUE #(
      mandt       = sy-mandt
      audit_id    = cl_system_uuid=>create_uuid_c32_static( )
      sap_user    = sy-uname
      action_type = 'REJECT_SO'
      so_number   = lv_so_number
      status      = 'SUCCESS'
      remarks     = ls_key-%param-rejection_code
      created_at  = lv_timestamp
    ) ).

    APPEND VALUE #( %tky = ls_key-%tky ) TO result.
  ENDLOOP.
ENDMETHOD.
ENDCLASS.
