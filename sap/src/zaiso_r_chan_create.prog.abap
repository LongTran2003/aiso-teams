REPORT zaiso_r_chan_create.

TABLES zaiso_chan.

PARAMETERS p_chan TYPE zaiso_chan-teams_chan OBLIGATORY.
PARAMETERS p_vkorg TYPE zaiso_chan-sales_org OBLIGATORY.
PARAMETERS p_type TYPE zaiso_chan-sub_type DEFAULT 'KPI' OBLIGATORY.
PARAMETERS p_active TYPE zaiso_chan-is_active DEFAULT abap_true.

DATA ls_chan TYPE zaiso_chan.

START-OF-SELECTION.

  SELECT SINGLE chan_id
    FROM zaiso_chan
    WHERE teams_chan = @p_chan
      AND sales_org  = @p_vkorg
      AND sub_type   = @p_type
    INTO @DATA(lv_existing_id).

  IF sy-subrc = 0.
    MESSAGE 'Subscription already exists.' TYPE 'S' DISPLAY LIKE 'W'.
    RETURN.
  ENDIF.

  TRY.
      ls_chan-chan_id = cl_system_uuid=>create_uuid_c32_static( ).
    CATCH cx_uuid_error.
      MESSAGE 'Could not create UUID.' TYPE 'E'.
  ENDTRY.

  ls_chan-mandt      = sy-mandt.
  ls_chan-teams_chan = p_chan.
  ls_chan-sales_org  = p_vkorg.
  ls_chan-sub_type   = p_type.
  ls_chan-is_active  = p_active.
  ls_chan-created_by = sy-uname.

  GET TIME STAMP FIELD ls_chan-created_at.

  INSERT zaiso_chan FROM ls_chan.

  IF sy-subrc = 0.
    COMMIT WORK.
    MESSAGE 'Teams channel subscription created.' TYPE 'S'.
  ELSE.
    ROLLBACK WORK.
    MESSAGE 'Could not create Teams channel subscription.' TYPE 'S' DISPLAY LIKE 'E'.
  ENDIF.
