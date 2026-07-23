CLASS lcl_buffer DEFINITION.
  PUBLIC SECTION.
    TYPES: BEGIN OF ty_release_reject,
             so_number      TYPE vbeln_va,
             action_type    TYPE string,   " 'RELEASE' hoặc 'REJECT'
             rejection_code TYPE bapisditm-reason_rej,
           END OF ty_release_reject.

    CLASS-DATA: gt_so_map_db     TYPE TABLE OF zaiso_so_map,
                gt_audit_db      TYPE TABLE OF zaiso_audit,
                gt_release_reject TYPE TABLE OF ty_release_reject.
ENDCLASS.

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

    METHODS approveOrder FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~approveOrder RESULT result.

    METHODS rejectApproval FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~rejectApproval RESULT result.

    METHODS reassignOwner FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~reassignOwner RESULT result.

    METHODS forceCancel FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~forceCancel RESULT result.

    METHODS forceRelease FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~forceRelease RESULT result.

    METHODS get_user_role
      IMPORTING iv_sap_user TYPE char100
      RETURNING VALUE(rv_role) TYPE zaiso_de_role.

    METHODS get_instance_authorizations FOR INSTANCE AUTHORIZATION
      IMPORTING keys REQUEST requested_authorizations FOR SalesOrder RESULT result.

ENDCLASS.

CLASS lhc_SalesOrder IMPLEMENTATION.

    METHOD approveorder.
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.
      DATA(lv_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_role <> 'MANAGER' AND lv_role <> 'ADMIN'.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
          TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message(
                           id       = '00'
                           number   = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1       = 'Only Manager/Admin can approve' ) )
          TO reported-salesorder.

        CONTINUE.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = lv_requesting_user
        actor_role  = lv_role
        action_type = 'APPROVE_SO'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( %tky     = ls_key-%tky
                      SoNumber = lv_so_number ) TO result.
    ENDLOOP.
  ENDMETHOD.

    METHOD rejectapproval.
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.
      DATA(lv_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_role <> 'MANAGER' AND lv_role <> 'ADMIN'.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
          TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message(
                           id       = '00'
                           number   = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1       = 'Only Manager/Admin can reject approval' ) )
          TO reported-salesorder.

        CONTINUE.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = lv_requesting_user
        actor_role  = lv_role
        action_type = 'REJECT_APPROVAL'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( %tky     = ls_key-%tky
                       SoNumber = lv_so_number ) TO result.
    ENDLOOP.
  ENDMETHOD.

    METHOD reassignowner.
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.
      DATA(lv_new_owner) = ls_key-%param-new_owner_id.
      DATA(lv_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_role <> 'MANAGER' AND lv_role <> 'ADMIN'.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
          TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message(
                           id       = '00'
                           number   = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1       = 'Only Manager/Admin can reassign' ) )
          TO reported-salesorder.

        CONTINUE.
      ENDIF.

      IF lv_new_owner IS INITIAL.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unspecific )
          TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message(
                           id       = '00'
                           number   = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1       = 'New owner is required' ) )
          TO reported-salesorder.

        CONTINUE.
      ENDIF.

      APPEND VALUE #(
        mandt     = sy-mandt
        so_number = lv_so_number
        sap_user  = lv_new_owner
      ) TO lcl_buffer=>gt_so_map_db.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = lv_requesting_user
        actor_role  = lv_role
        action_type = 'REASSIGN_SO'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        remarks     = lv_new_owner
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( %tky     = ls_key-%tky
                       SoNumber = lv_so_number ) TO result.
    ENDLOOP.
  ENDMETHOD.

    METHOD forcecancel.
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.
      DATA(lv_reason) = ls_key-%param-override_reason.
      DATA(lv_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_role <> 'ADMIN'.
        APPEND VALUE #( %tky = ls_key-%tky
                        %fail-cause = if_abap_behv=>cause-unauthorized )
               TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                        %msg = new_message(
                          id       = '00'
                          number   = '001'
                          severity = if_abap_behv_message=>severity-error
                          v1       = 'Only Admin can force cancel' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      IF lv_reason IS INITIAL.
        APPEND VALUE #( %tky = ls_key-%tky
                        %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                        %msg = new_message(
                          id       = '00'
                          number   = '001'
                          severity = if_abap_behv_message=>severity-error
                          v1       = 'Override reason is required' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt      = sy-mandt
        audit_id   = lv_audit_id
        sap_user   = lv_requesting_user
        action_type = 'FORCE_CANCEL'
        so_number  = lv_so_number
        status     = 'SUCCESS'
        created_at = lv_timestamp
        actor_role = lv_role
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( %tky = ls_key-%tky ) TO result.
    ENDLOOP.
  ENDMETHOD.

    METHOD forcerelease.
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.
      DATA(lv_reason) = ls_key-%param-override_reason.
      DATA(lv_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_role <> 'ADMIN'.
        APPEND VALUE #( %tky = ls_key-%tky
                        %fail-cause = if_abap_behv=>cause-unauthorized )
               TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                        %msg = new_message(
                          id       = '00'
                          number   = '001'
                          severity = if_abap_behv_message=>severity-error
                          v1       = 'Only Admin can force release' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      IF lv_reason IS INITIAL.
        APPEND VALUE #( %tky = ls_key-%tky
                        %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-salesorder.

        APPEND VALUE #( %tky = ls_key-%tky
                        %msg = new_message(
                          id       = '00'
                          number   = '001'
                          severity = if_abap_behv_message=>severity-error
                          v1       = 'Override reason is required' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt      = sy-mandt
        audit_id   = lv_audit_id
        sap_user   = lv_requesting_user
        action_type = 'FORCE_RELEASE'
        so_number  = lv_so_number
        status     = 'SUCCESS'
        created_at = lv_timestamp
        actor_role = lv_role
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( %tky = ls_key-%tky ) TO result.
    ENDLOOP.
  ENDMETHOD.

  METHOD get_user_role.
    SELECT SINGLE role
      FROM zaiso_user_role
      WHERE sap_user   = @iv_sap_user
        AND valid_from <= @sy-datum
        AND valid_to   >= @sy-datum
      INTO @rv_role.

    IF sy-subrc <> 0.
      rv_role = 'EMPLOYEE'.
    ENDIF.
  ENDMETHOD.

  METHOD createSalesOrder.
    DATA: ls_header_in  TYPE bapisdhead1,
          ls_header_inx TYPE bapisdhead1x,
          lt_partners   TYPE TABLE OF bapiparnr,
          lt_items_in   TYPE TABLE OF bapiitemin,
          lt_return     TYPE TABLE OF bapiret2,
          lv_so_number  TYPE vbeln_va,
          lv_timestamp  TYPE c LENGTH 14,
          lv_audit_id   TYPE sysuuid_c32.

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
        APPEND VALUE #( %cid = ls_key-%cid %fail-cause = if_abap_behv=>cause-unspecific ) TO failed-salesorder.
        CONTINUE.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = sy-uname
        action_type = 'CREATE_SO'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

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
          lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
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
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-salesorder.
        CONTINUE.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = sy-uname
        action_type = 'CANCEL_SO'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( %tky = ls_key-%tky ) TO result.
    ENDLOOP.
  ENDMETHOD.

  METHOD updatereference.
  DATA: ls_header_in  TYPE bapisdh1,
        ls_header_inx TYPE bapisdh1x,
        lt_return     TYPE TABLE OF bapiret2,
        lv_timestamp  TYPE c LENGTH 14,
        lv_audit_id   TYPE sysuuid_c32,
        lv_so_number  TYPE vbeln_va.

  LOOP AT keys INTO DATA(ls_key).
    lv_so_number = |{ ls_key-SoNumber ALPHA = IN }|.

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
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-salesorder.
        CONTINUE.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = sy-uname
        action_type = 'UPDATE_REF_SO'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( %tky = ls_key-%tky ) TO result.
    ENDLOOP.
  ENDMETHOD.

  METHOD read.
  DATA(lt_keys) = keys.

  SELECT * FROM vbak
    FOR ALL ENTRIES IN @lt_keys
    WHERE vbeln = @lt_keys-SoNumber
    INTO TABLE @DATA(lt_vbak).

  IF lt_vbak IS NOT INITIAL.
    SELECT vbeln, cmgst, fkstk FROM vbuk
      FOR ALL ENTRIES IN @lt_vbak
      WHERE vbeln = @lt_vbak-vbeln
      INTO TABLE @DATA(lt_vbuk).

    SELECT vbeln, abgru FROM vbap
      FOR ALL ENTRIES IN @lt_vbak
      WHERE vbeln = @lt_vbak-vbeln
      INTO TABLE @DATA(lt_vbap).
  ENDIF.

  LOOP AT lt_vbak INTO DATA(ls_vbak).
    READ TABLE lt_vbuk INTO DATA(ls_vbuk) WITH KEY vbeln = ls_vbak-vbeln.
    IF sy-subrc <> 0.
      CLEAR ls_vbuk.
    ENDIF.

    DATA(lv_all_rejected) = abap_true.
    DATA(lv_has_item) = abap_false.
    LOOP AT lt_vbap INTO DATA(ls_vbap) WHERE vbeln = ls_vbak-vbeln.
      lv_has_item = abap_true.
      IF ls_vbap-abgru IS INITIAL.
        lv_all_rejected = abap_false.
        EXIT.
      ENDIF.
    ENDLOOP.

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
      CreditStatus  = ls_vbuk-cmgst
      DeliveryBlock = ls_vbak-lifsk
      BillingStatus = ls_vbuk-fkstk
      IsCancelled   = COND #( WHEN lv_has_item = abap_true AND lv_all_rejected = abap_true
                               THEN 'X' ELSE '' )
    ) TO result.
  ENDLOOP.
ENDMETHOD.

  METHOD lock.
  ENDMETHOD.

  METHOD releaseorder.
  LOOP AT keys INTO DATA(ls_key).
    DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

    SELECT SINGLE teams_user_id FROM zaiso_so_map
      INTO @DATA(lv_owner)
      WHERE so_number = @lv_so_number.

    IF lv_owner IS NOT INITIAL AND lv_owner <> ls_key-%param-requesting_teams_user.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-salesorder.
      APPEND VALUE #( %tky = ls_key-%tky
                       %msg = new_message( id       = '00'
                                            number   = '001'
                                            severity = if_abap_behv_message=>severity-error
                                            v1       = 'Order owned by another user' ) )
             TO reported-salesorder.
      CONTINUE.
    ENDIF.

    " Chỉ buffer, KHÔNG gọi BAPI ở đây
    APPEND VALUE #( so_number   = lv_so_number
                     action_type = 'RELEASE' ) TO lcl_buffer=>gt_release_reject.

    APPEND VALUE #( %tky     = ls_key-%tky
                     SoNumber = lv_so_number ) TO result.
  ENDLOOP.
ENDMETHOD.

  METHOD forwardorder.
  DATA: lv_timestamp TYPE c LENGTH 14,
        lv_audit_id  TYPE sysuuid_c32,
        lv_so_number TYPE vbeln_va.

  LOOP AT keys INTO DATA(ls_key).
    DATA(ls_param) = ls_key-%param.
    lv_so_number = |{ ls_key-SoNumber ALPHA = IN }|.

    SELECT SINGLE teams_user_id FROM zaiso_so_map
      INTO @DATA(lv_owner)
      WHERE so_number = @lv_so_number.

    IF lv_owner IS NOT INITIAL AND lv_owner <> ls_param-requesting_teams_user.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-SalesOrder.
      APPEND VALUE #( %tky = ls_key-%tky
                   %msg = new_message( id       = '00'
                                        number   = '001'
                                        severity = if_abap_behv_message=>severity-error
                                        v1       = 'Order owned by another user' ) )
         TO reported-SalesOrder.
      CONTINUE.
    ENDIF.

    APPEND VALUE #(
      mandt         = sy-mandt
      so_number     = lv_so_number
      teams_user_id = ls_param-new_teams_user
    ) TO lcl_buffer=>gt_so_map_db.

    CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

    TRY.
        lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
      CATCH cx_uuid_error.
        CLEAR lv_audit_id.
    ENDTRY.

    APPEND VALUE #(
      mandt       = sy-mandt
      audit_id    = lv_audit_id
      sap_user    = sy-uname
      action_type = 'FORWARD_SO'
      so_number   = lv_so_number
      status      = 'SUCCESS'
      remarks     = ls_param-remarks
      created_at  = lv_timestamp
    ) TO lcl_buffer=>gt_audit_db.

    APPEND VALUE #( %tky     = ls_key-%tky
                     SoNumber = lv_so_number ) TO result.
  ENDLOOP.
ENDMETHOD.

  METHOD rejectorder.
  LOOP AT keys INTO DATA(ls_key).
    DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

    IF ls_key-%param-rejection_code <> '02' AND
       ls_key-%param-rejection_code <> '03' AND
       ls_key-%param-rejection_code <> '04'.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-SalesOrder.
      APPEND VALUE #( %tky = ls_key-%tky
                       %msg = new_message( id       = '00'
                                            number   = '001'
                                            severity = if_abap_behv_message=>severity-error
                                            v1       = 'Invalid rejection code' ) )
             TO reported-SalesOrder.
      CONTINUE.
    ENDIF.

    SELECT SINGLE uname FROM agr_users
      INTO @DATA(lv_auth_check)
      WHERE agr_name = 'ZROLE_AISO_BOT_RELEASER'
        AND uname    = @sy-uname.

    IF sy-subrc <> 0.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-SalesOrder.
      APPEND VALUE #( %tky = ls_key-%tky
                       %msg = new_message( id       = '00'
                                            number   = '001'
                                            severity = if_abap_behv_message=>severity-error
                                            v1       = 'Bot user missing releaser role' ) )
             TO reported-SalesOrder.
      CONTINUE.
    ENDIF.

    SELECT posnr FROM vbap
      INTO TABLE @DATA(lt_posnr)
      WHERE vbeln = @lv_so_number.

    IF lt_posnr IS INITIAL.
      APPEND VALUE #( %tky        = ls_key-%tky
                       %fail-cause = if_abap_behv=>cause-not_found )
             TO failed-SalesOrder.
      APPEND VALUE #( %tky = ls_key-%tky
                       %msg = new_message( id       = '00'
                                            number   = '001'
                                            severity = if_abap_behv_message=>severity-error
                                            v1       = 'Sales order items not found' ) )
             TO reported-SalesOrder.
      CONTINUE.
    ENDIF.

    " Chỉ buffer, KHÔNG gọi BAPI ở đây
    APPEND VALUE #( so_number      = lv_so_number
                     action_type    = 'REJECT'
                     rejection_code = ls_key-%param-rejection_code )
           TO lcl_buffer=>gt_release_reject.

    APPEND VALUE #( %tky     = ls_key-%tky
                     SoNumber = lv_so_number ) TO result.
  ENDLOOP.
ENDMETHOD.

  METHOD get_instance_authorizations.
  " Ví dụ tối thiểu: cho phép tất cả các action nếu chưa có logic phân quyền cụ thể
  result = VALUE #( FOR ls_key IN keys
    ( %tky                   = ls_key-%tky
      %update                = if_abap_behv=>auth-allowed
      %action-releaseOrder   = if_abap_behv=>auth-allowed
      %action-forwardOrder   = if_abap_behv=>auth-allowed
      %action-rejectOrder    = if_abap_behv=>auth-allowed
      %action-approveOrder   = if_abap_behv=>auth-allowed
      %action-rejectApproval = if_abap_behv=>auth-allowed
      %action-reassignOwner  = if_abap_behv=>auth-allowed
      %action-forceCancel    = if_abap_behv=>auth-allowed
      %action-forceRelease   = if_abap_behv=>auth-allowed ) ).
ENDMETHOD.
ENDCLASS.

CLASS lsc_zbp_i_aiso_so_header DEFINITION INHERITING FROM cl_abap_behavior_saver_failed.
  PROTECTED SECTION.
    METHODS save REDEFINITION.
    METHODS cleanup REDEFINITION.
ENDCLASS.

CLASS lsc_zbp_i_aiso_so_header IMPLEMENTATION.
  METHOD save.
    DATA: ls_header_in  TYPE bapisdh1,
          ls_header_inx TYPE bapisdh1x,
          lt_items_in   TYPE TABLE OF bapisditm,
          lt_items_inx  TYPE TABLE OF bapisditmx,
          lt_return     TYPE TABLE OF bapiret2,
          lv_timestamp  TYPE c LENGTH 14,
          lv_audit_id   TYPE sysuuid_c32,
          lv_status     TYPE string.

    LOOP AT lcl_buffer=>gt_release_reject INTO DATA(ls_rr).
      CLEAR: ls_header_in, ls_header_inx, lt_items_in, lt_items_inx, lt_return.
      ls_header_inx-updateflag = 'U'.

      IF ls_rr-action_type = 'RELEASE'.
        ls_header_in-dlv_block  = ''.
        ls_header_inx-dlv_block = 'X'.

        CALL FUNCTION 'BAPI_SALESORDER_CHANGE'
          EXPORTING
            salesdocument    = ls_rr-so_number
            order_header_in  = ls_header_in
            order_header_inx = ls_header_inx
          TABLES
            return           = lt_return.

      ELSEIF ls_rr-action_type = 'REJECT'.
        SELECT posnr FROM vbap
          INTO TABLE @DATA(lt_posnr)
          WHERE vbeln = @ls_rr-so_number.

        LOOP AT lt_posnr INTO DATA(ls_posnr).
          APPEND VALUE #( itm_number = ls_posnr-posnr
                           reason_rej = ls_rr-rejection_code ) TO lt_items_in.
          APPEND VALUE #( itm_number = ls_posnr-posnr
                           reason_rej = 'X' ) TO lt_items_inx.
        ENDLOOP.

        CALL FUNCTION 'BAPI_SALESORDER_CHANGE'
          EXPORTING
            salesdocument    = ls_rr-so_number
            order_header_in  = ls_header_in
            order_header_inx = ls_header_inx
          TABLES
            order_item_in    = lt_items_in
            order_item_inx   = lt_items_inx
            return           = lt_return.
      ENDIF.

      " KHÔNG gọi BAPI_TRANSACTION_COMMIT — RAP tự commit sau save()

      READ TABLE lt_return WITH KEY type = 'E' INTO DATA(ls_error).
      IF sy-subrc = 0.
        lv_status = 'FAILED'.

        " Báo lỗi thật về OData — chỉ khả thi vì kế thừa cl_abap_behavior_saver_failed
        APPEND VALUE #( %tky-SoNumber = ls_rr-so_number
                         %fail-cause  = if_abap_behv=>cause-unspecific )
               TO failed-salesorder.
        APPEND VALUE #( %tky-SoNumber = ls_rr-so_number
                         %msg = new_message( id       = ls_error-id
                                              number   = ls_error-number
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = ls_error-message_v1
                                              v2       = ls_error-message_v2
                                              v3       = ls_error-message_v3
                                              v4       = ls_error-message_v4 ) )
               TO reported-salesorder.
      ELSE.
        lv_status = 'SUCCESS'.
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.
      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      APPEND VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = sy-uname
        action_type = COND #( WHEN ls_rr-action_type = 'RELEASE' THEN 'RELEASE_SO' ELSE 'REJECT_SO' )
        so_number   = ls_rr-so_number
        status      = lv_status
        remarks     = COND #( WHEN lv_status = 'FAILED' THEN ls_error-message ELSE ls_rr-rejection_code )
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.
    ENDLOOP.

    IF lcl_buffer=>gt_so_map_db IS NOT INITIAL.
      MODIFY zaiso_so_map FROM TABLE @lcl_buffer=>gt_so_map_db.
    ENDIF.

    IF lcl_buffer=>gt_audit_db IS NOT INITIAL.
      INSERT zaiso_audit FROM TABLE @lcl_buffer=>gt_audit_db.
    ENDIF.
  ENDMETHOD.

  METHOD cleanup.
    CLEAR: lcl_buffer=>gt_so_map_db, lcl_buffer=>gt_audit_db, lcl_buffer=>gt_release_reject.
  ENDMETHOD.
ENDCLASS.
