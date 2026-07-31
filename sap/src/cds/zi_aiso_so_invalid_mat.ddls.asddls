@AccessControl.authorizationCheck: #CHECK
@EndUserText.label: 'AISO - SO Invalid Material Count'
@Metadata.ignorePropagatedAnnotations: true

define view entity ZI_AISO_SO_INVALID_MAT
  as select from vbap as item
  left outer join mara as mat
    on item.matnr = mat.matnr
{
  key item.vbeln                                          as SoNumber,
      sum( case when mat.matnr is null then 1 else 0 end ) as InvalidMaterialCount
}
group by item.vbeln
