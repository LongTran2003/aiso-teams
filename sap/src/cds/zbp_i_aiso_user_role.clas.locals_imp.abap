CLASS lhc_UserRole DEFINITION INHERITING FROM cl_abap_behavior_handler.
  PRIVATE SECTION.
    METHODS syncuserrole FOR MODIFY
      IMPORTING keys FOR ACTION UserRole~syncUserRole.

    METHODS get_user_role
      IMPORTING iv_sap_user TYPE char100
      RETURNING VALUE(rv_role) TYPE zaiso_de_role.

    METHODS get_global_authorizations FOR GLOBAL AUTHORIZATION
      IMPORTING REQUEST requested_authorizations FOR UserRole RESULT result.
ENDCLASS.

CLASS lhc_UserRole IMPLEMENTATION.

  METHOD get_global_authorizations.
    result-%action-syncUserRole = if_abap_behv=>auth-allowed.
  ENDMETHOD.

  METHOD syncuserrole.
    DATA: lv_timestamp TYPE c LENGTH 14,
          lv_audit_id  TYPE sysuuid_c32.

    LOOP AT keys INTO DATA(ls_key).
      DATA(lv_requesting_user) = ls_key-%param-requesting_teams_user.
      DATA(lv_caller_role)     = get_user_role( iv_sap_user = lv_requesting_user ).

      IF lv_caller_role <> 'ADMIN'.
        APPEND VALUE #( %cid        = ls_key-%cid
                         %fail-cause = if_abap_behv=>cause-unauthorized )
               TO failed-userrole.
        APPEND VALUE #( %cid = ls_key-%cid
                         %msg = new_message(
                           id       = '00'
                           number   = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1       = 'Only Admin can sync user role' ) )
               TO reported-userrole.
        CONTINUE.
      ENDIF.

      DATA(lv_target_user) = ls_key-%param-sap_user.
      DATA(lv_new_role)    = ls_key-%param-new_role.

      IF lv_new_role <> 'EMPLOYEE' AND lv_new_role <> 'MANAGER' AND lv_new_role <> 'ADMIN'.
        APPEND VALUE #( %cid        = ls_key-%cid
                         %fail-cause = if_abap_behv=>cause-unspecific )
               TO failed-userrole.
        APPEND VALUE #( %cid = ls_key-%cid
                         %msg = new_message(
                           id       = '00'
                           number   = '001'
                           severity = if_abap_behv_message=>severity-error
                           v1       = 'Invalid role value' ) )
               TO reported-userrole.
        CONTINUE.
      ENDIF.

      UPDATE zaiso_user_role
        SET role       = @lv_new_role,
            vkorg      = @ls_key-%param-sales_org,
            valid_from = @sy-datum,
            valid_to   = '99991231'
        WHERE sap_user = @lv_target_user.

      IF sy-subrc <> 0.
        INSERT zaiso_user_role FROM @( VALUE #(
          mandt      = sy-mandt
          sap_user   = lv_target_user
          role       = lv_new_role
          vkorg      = ls_key-%param-sales_org
          valid_from = sy-datum
          valid_to   = '99991231'
        ) ).
      ENDIF.

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.
      TRY.
          lv_audit_id = cl_system_uuid=>create_uuid_c32_static( ).
        CATCH cx_uuid_error.
          CLEAR lv_audit_id.
      ENDTRY.

      INSERT zaiso_audit FROM @( VALUE #(
        mandt       = sy-mandt
        audit_id    = lv_audit_id
        sap_user    = lv_requesting_user
        actor_role  = lv_caller_role
        action_type = 'SYNC_USER_ROLE'
        so_number   = ''
        status      = 'SUCCESS'
        remarks     = |{ lv_target_user } -> { lv_new_role }|
        created_at  = lv_timestamp
      ) ).

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

ENDCLASS.
