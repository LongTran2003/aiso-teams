*&---------------------------------------------------------------------*
*& Report zaiso_r_check_invalid_matnr
*&---------------------------------------------------------------------*
*&
*&---------------------------------------------------------------------*
REPORT zaiso_r_check_invalid_matnr.

TYPES: BEGIN OF ty_result,
         vbeln TYPE vbap-vbeln,
         posnr TYPE vbap-posnr,
         matnr TYPE vbap-matnr,
       END OF ty_result.

DATA: lt_distinct_matnr TYPE SORTED TABLE OF vbap-matnr WITH UNIQUE KEY table_line,
      lt_valid_matnr    TYPE SORTED TABLE OF mara-matnr WITH UNIQUE KEY table_line,
      lt_result         TYPE TABLE OF ty_result.

" Lấy toàn bộ material distinct đang được dùng trong VBAP
SELECT DISTINCT matnr FROM vbap
  INTO TABLE @lt_distinct_matnr
  WHERE matnr <> ''.

" Lấy danh sách material thật sự tồn tại trong MARA
IF lt_distinct_matnr IS NOT INITIAL.
  SELECT matnr FROM mara
    INTO TABLE @lt_valid_matnr
    FOR ALL ENTRIES IN @lt_distinct_matnr
    WHERE matnr = @lt_distinct_matnr-table_line.
ENDIF.

" Với mỗi material không tồn tại trong MARA, tìm hết các SO/item đang dùng nó
LOOP AT lt_distinct_matnr INTO DATA(lv_matnr).
  IF NOT line_exists( lt_valid_matnr[ table_line = lv_matnr ] ).
    SELECT vbeln, posnr, matnr FROM vbap
      APPENDING CORRESPONDING FIELDS OF TABLE @lt_result
      WHERE matnr = @lv_matnr.
  ENDIF.
ENDLOOP.

IF lt_result IS INITIAL.
  WRITE: / 'Không có material nào bị thiếu trong MARA.'.
ELSE.
  WRITE: / 'Danh sách SO/Item dùng material KHÔNG tồn tại trong MARA:'.
  ULINE.
  LOOP AT lt_result INTO DATA(ls_result).
    WRITE: / ls_result-vbeln, ls_result-posnr, ls_result-matnr.
  ENDLOOP.
ENDIF.
