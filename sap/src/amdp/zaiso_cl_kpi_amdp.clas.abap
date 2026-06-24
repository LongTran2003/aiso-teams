CLASS zaiso_cl_kpi_amdp DEFINITION
  PUBLIC
  FINAL
  CREATE PUBLIC.

  PUBLIC SECTION.
    INTERFACES if_amdp_marker_hdb.

    TYPES: BEGIN OF ty_revenue_result,
             sales_org     TYPE vkorg,
             year_month    TYPE char6,
             total_revenue TYPE p LENGTH 8 DECIMALS 2,
             order_count   TYPE i,
           END OF ty_revenue_result.

    TYPES: tt_revenue_result TYPE STANDARD TABLE OF ty_revenue_result
                              WITH DEFAULT KEY.

    " Tính tổng revenue theo SalesOrg + Year-Month
    METHODS get_revenue_by_period
      IMPORTING
        VALUE(iv_sales_org) TYPE vkorg
        VALUE(iv_period)    TYPE char6
      EXPORTING
        VALUE(et_result)    TYPE tt_revenue_result.

    " Đếm số SO theo status (Open / Completed / Rejected)
    METHODS get_so_count_by_status
      IMPORTING
        VALUE(iv_sales_org) TYPE vkorg
      EXPORTING
        VALUE(et_result)    TYPE tt_revenue_result.

ENDCLASS.

CLASS zaiso_cl_kpi_amdp IMPLEMENTATION.

  METHOD get_revenue_by_period
    BY DATABASE PROCEDURE FOR HDB
    LANGUAGE SQLSCRIPT
    OPTIONS READ-ONLY
    USING vbak.

    et_result =
      SELECT
        vkorg            AS sales_org,
        SUBSTRING(audat, 1, 6) AS year_month,
        SUM(netwr)       AS total_revenue,
        COUNT(*)         AS order_count
      FROM vbak
      WHERE vkorg = :iv_sales_org
        AND SUBSTRING(audat, 1, 6) = :iv_period
      GROUP BY vkorg, SUBSTRING(audat, 1, 6);

  ENDMETHOD.

  METHOD get_so_count_by_status
    BY DATABASE PROCEDURE FOR HDB
    LANGUAGE SQLSCRIPT
    OPTIONS READ-ONLY
    USING vbak.

    et_result =
      SELECT
        vkorg            AS sales_org,
        gbstk            AS year_month,
        SUM(netwr)       AS total_revenue,
        COUNT(*)         AS order_count
      FROM vbak
      WHERE vkorg = :iv_sales_org
      GROUP BY vkorg, gbstk;

  ENDMETHOD.

ENDCLASS.
