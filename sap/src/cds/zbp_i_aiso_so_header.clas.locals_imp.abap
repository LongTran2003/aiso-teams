CLASS lcl_buffer DEFINITION.
  PUBLIC SECTION.
    " --- Item structure cho UPDATE_SO ---
    TYPES: BEGIN OF ty_update_item,
             change_flag TYPE c LENGTH 1,
             item_no     TYPE posnr_va,
             material    TYPE matnr,
             order_qty   TYPE kwmeng,
             unit        TYPE meins,
           END OF ty_update_item,
           tt_update_item TYPE TABLE OF ty_update_item WITH EMPTY KEY.

    TYPES: BEGIN OF ty_release_reject,
             so_number       TYPE vbeln_va,
             action_type     TYPE string,
             rejection_code  TYPE bapisditm-reason_rej,
             requesting_user TYPE char100,           " ← THÊM
             new_reference   TYPE bapisdh1-purch_no_c, " ← THÊM
             req_date        TYPE sydatum,            " ← THÊM
             items           TYPE zaiso_tt_so_item_update,    " ← THÊM
           END OF ty_release_reject.

    " --- Create buffer types (giữ nguyên) ---
    TYPES: BEGIN OF ty_create_item,
             cid       TYPE string,
             cid_ref   TYPE string,
             material  TYPE matnr,
             plant     TYPE werks_d,
             order_qty TYPE kwmeng,
             unit      TYPE meins,
           END OF ty_create_item,
           tt_create_item TYPE TABLE OF ty_create_item WITH EMPTY KEY.

    TYPES: BEGIN OF ty_create_header,
             cid             TYPE string,
             requesting_user TYPE char100,
             customer        TYPE kunnr,
             doc_type        TYPE auart,
             sales_org       TYPE vkorg,
             dist_channel    TYPE vtweg,
             division        TYPE spart,
             currency        TYPE waers,
           END OF ty_create_header,
           tt_create_header TYPE TABLE OF ty_create_header WITH EMPTY KEY.

    CLASS-DATA: gt_so_map_db      TYPE TABLE OF zaiso_so_map,
                gt_audit_db       TYPE TABLE OF zaiso_audit,
                gt_release_reject TYPE TABLE OF ty_release_reject,
                gt_create_header  TYPE tt_create_header,
                gt_create_items   TYPE tt_create_item.
ENDCLASS.
CLASS lhc_SalesOrder DEFINITION INHERITING FROM cl_abap_behavior_handler.

  PRIVATE SECTION.

    METHODS create FOR MODIFY
      IMPORTING entities FOR CREATE SalesOrder.

    METHODS cba_Items FOR MODIFY
      IMPORTING entities_cba FOR CREATE SalesOrder\_Items.

    METHODS cancelOrder FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~cancelOrder RESULT result.

    METHODS updateReference FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~updateReference RESULT result.

    METHODS updateSalesOrder FOR MODIFY
      IMPORTING keys FOR ACTION SalesOrder~updateSalesOrder RESULT result.

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

    METHODS get_effective_role
      IMPORTING iv_sap_user  TYPE char100
                iv_sales_org TYPE vkorg OPTIONAL
      RETURNING VALUE(rv_role) TYPE zaiso_de_role.

    METHODS get_instance_authorizations FOR INSTANCE AUTHORIZATION
      IMPORTING keys REQUEST requested_authorizations FOR SalesOrder RESULT result.

    METHODS check_delegation_limit
  IMPORTING iv_sap_user   TYPE char100
            iv_sales_org  TYPE vkorg
            iv_net_amount TYPE netwr
            iv_currency   TYPE waerk
  RETURNING VALUE(rv_exceeded) TYPE abap_bool.

ENDCLASS.

CLASS lhc_SalesOrder IMPLEMENTATION.

  METHOD approveorder.
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.

      SELECT SINGLE vkorg FROM vbak
        INTO @DATA(lv_vkorg)
        WHERE vbeln = @lv_so_number.

      DATA(lv_role) = get_effective_role( iv_sap_user = lv_requesting_user iv_sales_org = lv_vkorg ).
      DATA(lv_base_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_role <> 'MANAGER' AND lv_role <> 'ADMIN'.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
          TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Only Manager/Admin can approve' ) )
          TO reported-salesorder.
        CONTINUE.
      ENDIF.
      " --- Defense-in-depth: nếu đang duyệt qua delegation, check giới hạn tiền ---
    IF lv_base_role <> 'MANAGER' AND lv_base_role <> 'ADMIN'.
      SELECT SINGLE netwr, waerk FROM vbak
        INTO @DATA(ls_amount_check)
        WHERE vbeln = @lv_so_number.

      DATA(lv_limit_exceeded) = check_delegation_limit(
        iv_sap_user   = lv_requesting_user
        iv_sales_org  = lv_vkorg
        iv_net_amount = ls_amount_check-netwr
        iv_currency   = ls_amount_check-waerk ).

      IF lv_limit_exceeded = abap_true.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Release is not allowed: order amount exceeds your delegation limit' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.
    ENDIF.

      SELECT posnr, matnr FROM vbap
        INTO TABLE @DATA(lt_posnr)
        WHERE vbeln = @lv_so_number.

      IF lt_posnr IS INITIAL.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-not_found )
          TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Sales order items not found' ) )
          TO reported-salesorder.
        CONTINUE.
      ENDIF.

      DATA(lv_has_invalid_material) = abap_false.
      LOOP AT lt_posnr INTO DATA(ls_check_item).
        SELECT SINGLE matnr FROM mara
          INTO @DATA(lv_matnr_exists)
          WHERE matnr = @ls_check_item-matnr.
        IF sy-subrc <> 0.
          lv_has_invalid_material = abap_true.
          EXIT.
        ENDIF.
      ENDLOOP.

      IF lv_has_invalid_material = abap_true.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unspecific )
          TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Order has invalid material master data' ) )
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
        remarks     = COND #( WHEN lv_base_role <> 'MANAGER' AND lv_base_role <> 'ADMIN'
                               THEN 'Approved via delegation' ELSE '' )
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

      APPEND VALUE #( so_number   = lv_so_number
                       action_type = 'RELEASE' ) TO lcl_buffer=>gt_release_reject.

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

      SELECT SINGLE vkorg FROM vbak
        INTO @DATA(lv_vkorg)
        WHERE vbeln = @lv_so_number.

      DATA(lv_role) = get_effective_role( iv_sap_user = lv_requesting_user iv_sales_org = lv_vkorg ).
      DATA(lv_base_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_role <> 'MANAGER' AND lv_role <> 'ADMIN'.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
          TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
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
        remarks     = COND #( WHEN lv_base_role <> 'MANAGER' AND lv_base_role <> 'ADMIN'
                               THEN 'Rejected via delegation' ELSE '' )
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

  METHOD get_effective_role.
    rv_role = get_user_role( iv_sap_user = iv_sap_user ).

    IF rv_role = 'MANAGER' OR rv_role = 'ADMIN'.
      RETURN.
    ENDIF.

    SELECT SINGLE delegate_user FROM zaiso_delegation
      INTO @DATA(lv_delegate_exists)
      WHERE delegate_user = @iv_sap_user
        AND sales_org     = @iv_sales_org
        AND status        = 'A'
        AND valid_from   <= @sy-datum
        AND valid_to     >= @sy-datum.

    IF sy-subrc = 0.
      rv_role = 'MANAGER'.
    ENDIF.
  ENDMETHOD.

  METHOD create.
  LOOP AT entities INTO DATA(ls_entity).

    " ══ Validate requesting user ══
    IF ls_entity-RequestingTeamsUser IS INITIAL.
      APPEND VALUE #( %cid        = ls_entity-%cid
                       %fail-cause = if_abap_behv=>cause-unauthorized )
             TO failed-salesorder.
      APPEND VALUE #( %cid = ls_entity-%cid
                       %msg = new_message(
                         id = '00' number = '001'
                         severity = if_abap_behv_message=>severity-error
                         v1 = 'Requesting user is required' ) )
             TO reported-salesorder.
      CONTINUE.
    ENDIF.

    " ══ Validate customer + sales area ══
    DATA(lv_customer) = |{ ls_entity-Customer ALPHA = IN }|.
    DATA: lv_debug_timestamp TYPE c LENGTH 14.
CONCATENATE sy-datum sy-uzeit INTO lv_debug_timestamp.

TRY.
    DATA(lv_debug_id) = cl_system_uuid=>create_uuid_c32_static( ).
  CATCH cx_uuid_error.
    CLEAR lv_debug_id.
ENDTRY.
INSERT zaiso_audit FROM @( VALUE #(
  mandt       = sy-mandt
  audit_id    = lv_debug_id
  sap_user    = sy-uname
  action_type = 'DEBUG_CREATE'
  so_number   = ''
  status      = 'DEBUG'
  remarks     = |CUST=[{ lv_customer }] SO=[{ ls_entity-SalesOrg }] DC=[{ ls_entity-DistChannel }] DV=[{ ls_entity-Division }]|
  created_at  = lv_debug_timestamp
) ).
    SELECT SINGLE kunnr FROM knvv
      INTO @DATA(lv_cust_valid)
      WHERE kunnr = @lv_customer
        AND vkorg = @ls_entity-SalesOrg
        AND vtweg = @ls_entity-DistChannel
        AND spart = @ls_entity-Division.

    IF sy-subrc <> 0.
      APPEND VALUE #( %cid        = ls_entity-%cid
                       %fail-cause = if_abap_behv=>cause-not_found )
             TO failed-salesorder.
      APPEND VALUE #( %cid = ls_entity-%cid
                       %msg = new_message(
                         id = '00' number = '001'
                         severity = if_abap_behv_message=>severity-error
                         v1 = |Customer { lv_customer } not valid for sales area| ) )
             TO reported-salesorder.
      CONTINUE.
    ENDIF.

    " ══ Buffer header (BAPI sẽ gọi trong adjust_numbers) ══
    APPEND VALUE #(
      cid             = ls_entity-%cid
      requesting_user = ls_entity-RequestingTeamsUser
      customer        = lv_customer
      doc_type        = ls_entity-DocType
      sales_org       = ls_entity-SalesOrg
      dist_channel    = ls_entity-DistChannel
      division        = ls_entity-Division
      currency        = ls_entity-Currency
    ) TO lcl_buffer=>gt_create_header.

    " ══ Register mapped (chưa có key, late numbering gán sau) ══
    APPEND VALUE #( %cid = ls_entity-%cid ) TO mapped-salesorder.

  ENDLOOP.
ENDMETHOD.

  METHOD cba_Items.
  LOOP AT entities_cba INTO DATA(ls_header_cba).
    LOOP AT ls_header_cba-%target INTO DATA(ls_item).

      " ══ Validate material ══
      SELECT SINGLE matnr FROM mara
        INTO @DATA(lv_matnr_check)
        WHERE matnr = @ls_item-Material.

      IF sy-subrc <> 0.
        APPEND VALUE #( %cid = ls_item-%cid
                         %fail-cause = if_abap_behv=>cause-not_found )
               TO failed-salesorderitem.
        APPEND VALUE #( %cid = ls_item-%cid
                         %msg = new_message(
                           id = '00' number = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1 = |Material { ls_item-Material } does not exist| ) )
               TO reported-salesorderitem.
        CONTINUE.
      ENDIF.

      " ══ Validate material-plant ══
      SELECT SINGLE werks FROM marc
        INTO @DATA(lv_plant_check)
        WHERE matnr = @ls_item-Material
          AND werks = @ls_item-Plant.

      IF sy-subrc <> 0.
        APPEND VALUE #( %cid = ls_item-%cid
                         %fail-cause = if_abap_behv=>cause-not_found )
               TO failed-salesorderitem.
        APPEND VALUE #( %cid = ls_item-%cid
                         %msg = new_message(
                           id = '00' number = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1 = |Material not extended to plant { ls_item-Plant }| ) )
               TO reported-salesorderitem.
        CONTINUE.
      ENDIF.

      " ══ Buffer item ══
      APPEND VALUE #(
        cid       = ls_item-%cid
        cid_ref   = ls_header_cba-%cid_ref    " link tới header's %cid
        material  = ls_item-Material
        plant     = ls_item-Plant
        order_qty = ls_item-OrderQty
        unit      = ls_item-Unit
      ) TO lcl_buffer=>gt_create_items.

      " ══ Register mapped ══
      APPEND VALUE #( %cid = ls_item-%cid ) TO mapped-salesorderitem.

    ENDLOOP.
  ENDLOOP.
ENDMETHOD.

  METHOD cancelorder.
    " FIX: chỉ validate + buffer. KHÔNG gọi BAPI_SALESORDER_CHANGE trực tiếp ở đây
    " (vi phạm buffer pattern -> gây dump BEHAVIOR_ILLEGAL_STATEMENT).
    " Thực thi BAPI thật chuyển sang save().
    DATA: lv_requesting_user TYPE char100.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      lv_requesting_user = ls_key-%param-requesting_teams_user.
      DATA(lv_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      SELECT SINGLE sap_user FROM zaiso_so_map
        INTO @DATA(lv_owner)
        WHERE so_number = @lv_so_number.

      IF lv_role = 'EMPLOYEE' AND lv_owner IS NOT INITIAL AND lv_owner <> lv_requesting_user.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Only owner, Manager, or Admin can cancel this order' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      SELECT posnr, matnr FROM vbap
        INTO TABLE @DATA(lt_posnr)
        WHERE vbeln = @lv_so_number.

      IF lt_posnr IS INITIAL.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-not_found )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Sales order items not found' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      DATA(lv_has_invalid_material) = abap_false.
      LOOP AT lt_posnr INTO DATA(ls_check_item).
        SELECT SINGLE matnr FROM mara
          INTO @DATA(lv_matnr_exists)
          WHERE matnr = @ls_check_item-matnr.
        IF sy-subrc <> 0.
          lv_has_invalid_material = abap_true.
          EXIT.
        ENDIF.
      ENDLOOP.

      IF lv_has_invalid_material = abap_true.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Order has invalid material master data' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      APPEND VALUE #( so_number       = lv_so_number
                       action_type     = 'CANCEL'
                       requesting_user = lv_requesting_user )
             TO lcl_buffer=>gt_release_reject.

      APPEND VALUE #( %tky = ls_key-%tky ) TO result.
    ENDLOOP.
  ENDMETHOD.

  METHOD updatereference.
    " FIX: chỉ validate + buffer. KHÔNG gọi BAPI trực tiếp (buffer pattern).
    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.

      SELECT SINGLE sap_user FROM zaiso_so_map
        INTO @DATA(lv_owner)
        WHERE so_number = @lv_so_number.

      IF lv_owner IS NOT INITIAL AND lv_owner <> lv_requesting_user.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message(
                           id       = '00'
                           number   = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1       = 'Only order owner can update reference' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      APPEND VALUE #( so_number       = lv_so_number
                       action_type     = 'UPDATE_REF'
                       requesting_user = lv_requesting_user
                       new_reference   = ls_key-%param-new_reference )
             TO lcl_buffer=>gt_release_reject.

      APPEND VALUE #( %tky = ls_key-%tky ) TO result.
    ENDLOOP.
  ENDMETHOD.

  METHOD updatesalesorder.
    " FIX: chỉ validate + buffer. KHÔNG gọi BAPI trực tiếp (buffer pattern).
    DATA: lv_requesting_user TYPE char100.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.
      lv_requesting_user = ls_key-%param-requesting_teams_user.
      DATA(lv_role) = get_user_role( iv_sap_user = lv_requesting_user ).

      SELECT SINGLE sap_user FROM zaiso_so_map
        INTO @DATA(lv_owner)
        WHERE so_number = @lv_so_number.

      IF lv_role = 'EMPLOYEE' AND lv_owner IS NOT INITIAL AND lv_owner <> lv_requesting_user.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unauthorized )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Only owner, Manager, or Admin can edit this order' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      DATA(lv_has_invalid_item) = abap_false.
      DATA(lv_invalid_detail)   = ''.
      LOOP AT ls_key-%param-items INTO DATA(ls_check_line) WHERE change_flag = 'I' OR change_flag = 'U'.
        SELECT SINGLE matnr FROM mara
          INTO @DATA(lv_matnr_exists)
          WHERE matnr = @ls_check_line-material.
        IF sy-subrc <> 0.
          lv_has_invalid_item = abap_true.
          lv_invalid_detail = |Material { ls_check_line-material } does not exist|.
          EXIT.
        ENDIF.
      ENDLOOP.

      IF lv_has_invalid_item = abap_true.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-not_found )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = lv_invalid_detail ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      APPEND VALUE #( so_number       = lv_so_number
                       action_type     = 'UPDATE_SO'
                       requesting_user = lv_requesting_user
                       new_reference   = ls_key-%param-new_reference
                       req_date        = ls_key-%param-requested_delivery_date
                       items           = ls_key-%param-items )
             TO lcl_buffer=>gt_release_reject.

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
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_so_number) = |{ ls_key-SoNumber ALPHA = IN }|.

      SELECT SINGLE sap_user FROM zaiso_so_map
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

      SELECT vbeln, matnr FROM vbap
        INTO TABLE @DATA(lt_check_items)
        WHERE vbeln = @lv_so_number.

      IF lt_check_items IS INITIAL.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-not_found )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Sales order items not found' ) )
               TO reported-salesorder.
        CONTINUE.
      ENDIF.

      DATA(lv_has_invalid_material) = abap_false.
      LOOP AT lt_check_items INTO DATA(ls_check_item).
        SELECT SINGLE matnr FROM mara
          INTO @DATA(lv_matnr_exists)
          WHERE matnr = @ls_check_item-matnr.
        IF sy-subrc <> 0.
          lv_has_invalid_material = abap_true.
          EXIT.
        ENDIF.
      ENDLOOP.

      IF lv_has_invalid_material = abap_true.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-salesorder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Order has invalid material master data' ) )
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
        sap_user    = ls_key-%param-requesting_teams_user
        action_type = 'RELEASE_REQUESTED'
        so_number   = lv_so_number
        status      = 'SUCCESS'
        created_at  = lv_timestamp
      ) TO lcl_buffer=>gt_audit_db.

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

      SELECT SINGLE sap_user FROM zaiso_so_map
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
        mandt     = sy-mandt
        so_number = lv_so_number
        sap_user  = ls_param-new_teams_user
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

      SELECT SINGLE abgru FROM tvag
        INTO @DATA(lv_valid_reason)
        WHERE abgru = @ls_key-%param-rejection_code.

      IF sy-subrc <> 0.
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

      SELECT posnr, matnr FROM vbap
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

      DATA(lv_has_invalid_material) = abap_false.
      LOOP AT lt_posnr INTO DATA(ls_check_item).
        SELECT SINGLE matnr FROM mara
          INTO @DATA(lv_matnr_exists)
          WHERE matnr = @ls_check_item-matnr.
        IF sy-subrc <> 0.
          lv_has_invalid_material = abap_true.
          EXIT.
        ENDIF.
      ENDLOOP.

      IF lv_has_invalid_material = abap_true.
        APPEND VALUE #( %tky        = ls_key-%tky
                         %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-SalesOrder.
        APPEND VALUE #( %tky = ls_key-%tky
                         %msg = new_message( id       = '00'
                                              number   = '001'
                                              severity = if_abap_behv_message=>severity-error
                                              v1       = 'Order has invalid material master data' ) )
               TO reported-SalesOrder.
        CONTINUE.
      ENDIF.

      APPEND VALUE #( so_number      = lv_so_number
                       action_type    = 'REJECT'
                       rejection_code = ls_key-%param-rejection_code )
             TO lcl_buffer=>gt_release_reject.

      APPEND VALUE #( %tky     = ls_key-%tky
                       SoNumber = lv_so_number ) TO result.
    ENDLOOP.
  ENDMETHOD.

  METHOD get_instance_authorizations.
    result = VALUE #( FOR ls_key IN keys
      ( %tky                     = ls_key-%tky
        %update                  = if_abap_behv=>auth-allowed
        %action-releaseOrder     = if_abap_behv=>auth-allowed
        %action-forwardOrder     = if_abap_behv=>auth-allowed
        %action-rejectOrder      = if_abap_behv=>auth-allowed
        %action-approveOrder     = if_abap_behv=>auth-allowed
        %action-rejectApproval   = if_abap_behv=>auth-allowed
        %action-reassignOwner    = if_abap_behv=>auth-allowed
        %action-forceCancel      = if_abap_behv=>auth-allowed
        %action-forceRelease     = if_abap_behv=>auth-allowed
        %action-updateSalesOrder = if_abap_behv=>auth-allowed ) ).
  ENDMETHOD.

  METHOD check_delegation_limit.
  rv_exceeded = abap_false.

  SELECT SINGLE max_amount, currency FROM zaiso_delegation
    INTO @DATA(ls_delegation)
    WHERE delegate_user = @iv_sap_user
      AND sales_org     = @iv_sales_org
      AND status        = 'A'
      AND valid_from   <= @sy-datum
      AND valid_to     >= @sy-datum.

  IF sy-subrc <> 0.
    RETURN.
  ENDIF.

  IF ls_delegation-max_amount IS INITIAL OR ls_delegation-max_amount <= 0.
    RETURN.
  ENDIF.

  " So sánh cùng currency; nếu khác currency, coi như không check (tránh so sai đơn vị tiền)
  IF ls_delegation-currency IS NOT INITIAL AND ls_delegation-currency <> iv_currency.
    RETURN.
  ENDIF.

  IF iv_net_amount > ls_delegation-max_amount.
    rv_exceeded = abap_true.
  ENDIF.
ENDMETHOD.

ENDCLASS.

CLASS lsc_zbp_i_aiso_so_header DEFINITION INHERITING FROM cl_abap_behavior_saver_failed.
  PROTECTED SECTION.
    METHODS save REDEFINITION.
    METHODS cleanup REDEFINITION.
    METHODS adjust_numbers REDEFINITION.
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

      CASE ls_rr-action_type.

        WHEN 'RELEASE'.
          ls_header_in-dlv_block  = ''.
          ls_header_inx-dlv_block = 'X'.

        WHEN 'REJECT'.
          SELECT posnr FROM vbap
            INTO TABLE @DATA(lt_posnr)
            WHERE vbeln = @ls_rr-so_number.

          LOOP AT lt_posnr INTO DATA(ls_posnr).
            APPEND VALUE #( itm_number = ls_posnr-posnr
                             reason_rej = ls_rr-rejection_code ) TO lt_items_in.
            APPEND VALUE #( itm_number = ls_posnr-posnr
                             reason_rej = 'X' ) TO lt_items_inx.
          ENDLOOP.

        WHEN 'CANCEL'.
          SELECT posnr FROM vbap
            INTO TABLE @lt_posnr
            WHERE vbeln = @ls_rr-so_number.

          LOOP AT lt_posnr INTO ls_posnr.
            APPEND VALUE #( itm_number = ls_posnr-posnr
                             reason_rej = 'Z1' ) TO lt_items_in.
            APPEND VALUE #( itm_number = ls_posnr-posnr
                             reason_rej = 'X' )  TO lt_items_inx.
          ENDLOOP.

        WHEN 'UPDATE_REF'.
          ls_header_in-purch_no_c  = ls_rr-new_reference.
          ls_header_inx-purch_no_c = 'X'.

        WHEN 'UPDATE_SO'.
  IF ls_rr-new_reference IS NOT INITIAL.
    ls_header_in-purch_no_c  = ls_rr-new_reference.
    ls_header_inx-purch_no_c = 'X'.
  ENDIF.
  IF ls_rr-req_date IS NOT INITIAL.
    ls_header_in-req_date_h  = ls_rr-req_date.
    ls_header_inx-req_date_h = 'X'.
  ENDIF.

  LOOP AT ls_rr-items INTO DATA(ls_line).
    CASE ls_line-change_flag.
      WHEN 'I'.
        APPEND VALUE #( itm_number = ls_line-item_no
                         material   = ls_line-material
                         plant      = ls_line-plant
                         target_qty = ls_line-order_qty
                         target_qu  = ls_line-unit )   TO lt_items_in.
        APPEND VALUE #( itm_number = ls_line-item_no
                         updateflag = 'I'
                         material   = 'X'
                         plant      = 'X'
                         target_qty = 'X'
                         target_qu  = 'X' )            TO lt_items_inx.
      WHEN 'U'.
        APPEND VALUE #( itm_number = ls_line-item_no
                         material   = ls_line-material
                         plant      = ls_line-plant
                         target_qty = ls_line-order_qty
                         target_qu  = ls_line-unit )   TO lt_items_in.
        APPEND VALUE #( itm_number = ls_line-item_no
                         updateflag = 'U'
                         material   = 'X'
                         plant      = COND #( WHEN ls_line-plant IS NOT INITIAL THEN 'X' ELSE '' )
                         target_qty = 'X'
                         target_qu  = 'X' )            TO lt_items_inx.
      WHEN 'D'.
        APPEND VALUE #( itm_number = ls_line-item_no ) TO lt_items_in.
        APPEND VALUE #( itm_number = ls_line-item_no
                         updateflag = 'D' )            TO lt_items_inx.
    ENDCASE.
  ENDLOOP.

      ENDCASE.

      CALL FUNCTION 'BAPI_SALESORDER_CHANGE'
        EXPORTING
          salesdocument    = ls_rr-so_number
          order_header_in  = ls_header_in
          order_header_inx = ls_header_inx
        TABLES
          order_item_in    = lt_items_in
          order_item_inx   = lt_items_inx
          return           = lt_return.

      READ TABLE lt_return WITH KEY type = 'E' INTO DATA(ls_error).
      IF sy-subrc = 0.
        lv_status = 'FAILED'.

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
        sap_user    = COND #( WHEN ls_rr-requesting_user IS NOT INITIAL THEN ls_rr-requesting_user ELSE sy-uname )
        action_type = SWITCH #( ls_rr-action_type
                                 WHEN 'RELEASE'    THEN 'RELEASE_SO'
                                 WHEN 'REJECT'      THEN 'REJECT_SO'
                                 WHEN 'CANCEL'      THEN 'CANCEL_SO'
                                 WHEN 'UPDATE_REF'  THEN 'UPDATE_REF_SO'
                                 WHEN 'UPDATE_SO'   THEN 'UPDATE_SO'
                                 ELSE ls_rr-action_type )
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
  CLEAR: lcl_buffer=>gt_so_map_db,
         lcl_buffer=>gt_audit_db,
         lcl_buffer=>gt_release_reject,
         lcl_buffer=>gt_create_header,     " ← THÊM
         lcl_buffer=>gt_create_items.      " ← THÊM
ENDMETHOD.

  METHOD adjust_numbers.
  DATA: ls_header_in       TYPE bapisdhd1,
        ls_header_inx      TYPE bapisdhd1x,
        lt_partners        TYPE TABLE OF bapiparnr,
        lt_items_in        TYPE TABLE OF bapisditm,
        lt_items_inx       TYPE TABLE OF bapisditmx,
        lt_schedules_in    TYPE TABLE OF bapischdl,
        lt_schedules_inx   TYPE TABLE OF bapischdlx,
        lt_return          TYPE TABLE OF bapiret2,
        lv_so_number       TYPE vbeln_va,
        lv_item_no         TYPE posnr_va,
        lv_timestamp       TYPE c LENGTH 14,
        lv_audit_id        TYPE sysuuid_c32.

  LOOP AT lcl_buffer=>gt_create_header INTO DATA(ls_hdr).

    CLEAR: ls_header_in, ls_header_inx, lt_partners,
           lt_items_in, lt_items_inx, lt_schedules_in, lt_schedules_inx,
           lt_return, lv_so_number.

    ls_header_in-doc_type   = ls_hdr-doc_type.
    ls_header_in-sales_org  = ls_hdr-sales_org.
    ls_header_in-distr_chan = ls_hdr-dist_channel.
    ls_header_in-division   = ls_hdr-division.
    ls_header_in-currency   = ls_hdr-currency.

    ls_header_inx-doc_type   = 'X'.
    ls_header_inx-sales_org  = 'X'.
    ls_header_inx-distr_chan = 'X'.
    ls_header_inx-division   = 'X'.
    ls_header_inx-currency   = 'X'.
    ls_header_inx-updateflag = 'I'.

    " FIX: ALPHA conversion cho customer
    APPEND VALUE #( partn_role = 'AG'
                     partn_numb = |{ ls_hdr-customer ALPHA = IN }| ) TO lt_partners.

    lv_item_no = 0.
    LOOP AT lcl_buffer=>gt_create_items INTO DATA(ls_itm)
         WHERE cid_ref = ls_hdr-cid.

      lv_item_no = lv_item_no + 10.

      " FIX: ALPHA conversion cho material
      APPEND VALUE #( itm_number = lv_item_no
                       material   = |{ ls_itm-material ALPHA = IN }|
                       plant      = ls_itm-plant
                       target_qty = ls_itm-order_qty
                       target_qu  = ls_itm-unit ) TO lt_items_in.

      APPEND VALUE #( itm_number = lv_item_no
                       material   = 'X'
                       plant      = 'X'
                       target_qty = 'X'
                       target_qu  = 'X' ) TO lt_items_inx.

      APPEND VALUE #( itm_number = lv_item_no
                       req_qty    = ls_itm-order_qty ) TO lt_schedules_in.

      APPEND VALUE #( itm_number = lv_item_no
                       req_qty    = 'X' ) TO lt_schedules_inx.
    ENDLOOP.

    CALL FUNCTION 'BAPI_SALESORDER_CREATEFROMDAT2'
      EXPORTING
        order_header_in    = ls_header_in
        order_header_inx   = ls_header_inx
      IMPORTING
        salesdocument      = lv_so_number
      TABLES
        return              = lt_return
        order_partners      = lt_partners
        order_items_in      = lt_items_in
        order_items_inx     = lt_items_inx
        order_schedules_in  = lt_schedules_in
        order_schedules_inx = lt_schedules_inx.

    READ TABLE lt_return WITH KEY type = 'E' INTO DATA(ls_error).
    IF sy-subrc = 0.
      APPEND VALUE #( %pid        = ls_hdr-cid
                       %fail-cause = if_abap_behv=>cause-unspecific )
             TO failed-salesorder.
      APPEND VALUE #( %pid = ls_hdr-cid
                       %msg = new_message(
                         id       = ls_error-id
                         number   = ls_error-number
                         severity = if_abap_behv_message=>severity-error
                         v1       = ls_error-message_v1
                         v2       = ls_error-message_v2
                         v3       = ls_error-message_v3
                         v4       = ls_error-message_v4 ) )
             TO reported-salesorder.
      CONTINUE.
    ENDIF.

    APPEND VALUE #( %pid           = ls_hdr-cid
                     %key-SoNumber = lv_so_number )
           TO mapped-salesorder.

    lv_item_no = 0.
    LOOP AT lcl_buffer=>gt_create_items INTO DATA(ls_itm_map)
         WHERE cid_ref = ls_hdr-cid.
      lv_item_no = lv_item_no + 10.
      APPEND VALUE #( %pid           = ls_itm_map-cid
                       %key-SoNumber = lv_so_number
                       %key-ItemNo   = lv_item_no )
             TO mapped-salesorderitem.
    ENDLOOP.

    APPEND VALUE #(
      mandt     = sy-mandt
      so_number = lv_so_number
      sap_user  = ls_hdr-requesting_user
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
      sap_user    = ls_hdr-requesting_user
      actor_role  = 'EMPLOYEE'
      action_type = 'CREATE_SO'
      so_number   = lv_so_number
      status      = 'SUCCESS'
      created_at  = lv_timestamp
    ) TO lcl_buffer=>gt_audit_db.

  ENDLOOP.
ENDMETHOD.
ENDCLASS.
