CLASS zaiso_cl_kpi_service DEFINITION
  PUBLIC
  FINAL
  CREATE PUBLIC.
PUBLIC SECTION.
    METHODS get_revenue_snapshot
      IMPORTING
        iv_sales_org TYPE vkorg
        iv_period    TYPE char6
      RETURNING VALUE(rs_result) TYPE zaiso_cl_kpi_amdp=>ty_revenue_result.
ENDCLASS.

CLASS zaiso_cl_kpi_service IMPLEMENTATION.

 METHOD get_revenue_snapshot.
    DATA: lo_amdp      TYPE REF TO zaiso_cl_kpi_amdp,
          lt_result    TYPE zaiso_cl_kpi_amdp=>tt_revenue_result,
          lv_timestamp TYPE c LENGTH 14.

    CREATE OBJECT lo_amdp.

    lo_amdp->get_revenue_by_period(
      EXPORTING
        iv_sales_org = iv_sales_org
        iv_period    = iv_period
      IMPORTING
        et_result    = lt_result
    ).

    IF lt_result IS NOT INITIAL.
      rs_result = lt_result[ 1 ].

      CONCATENATE sy-datum sy-uzeit INTO lv_timestamp.

      " Lưu snapshot vào table cache
      INSERT zaiso_kpi_snap FROM @( VALUE #(
        mandt       = sy-mandt
        snap_id     = cl_system_uuid=>create_uuid_c32_static( )
        kpi_type    = 'REVENUE'
        period_key  = iv_period
        sales_org   = iv_sales_org
        value_num   = rs_result-total_revenue
        computed_at = lv_timestamp
        valid_until = lv_timestamp
      ) ).
      COMMIT WORK.
    ENDIF.
  ENDMETHOD.

ENDCLASS.
