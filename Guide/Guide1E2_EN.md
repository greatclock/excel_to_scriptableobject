# Using nested types

## We Assume You've Finished

Finished the guidance [Reading data in Unity which fulfilled in a new xlsx file](./Guide1E1_EN.md)

## Guiding in Progress

1. Create a new sheet named "Parent" in "StudentsData.xlsx", and fill the sheet with data as the following image.
   
   ![Parent sheet in StudentsData.xlsx](./.images/img1.2-1.jpg)

2. Insert two columns in "Student" sheet, with the field name "father" and "mother", with their types both Parent. Fill the two columns with the key corresponding to the parent_id in "Parent" sheet.
   
   ![parent columns in Student sheet](./.images/img1.2-2.jpg)

3. Back to Unity and execute "Process Excel" again.
   
   ![Configuration window, focus on "Process Excel"](./.images/img1.2-3.jpg)

4. Modify TestExcelToSO.cs to test parents data.
   
   ![TestExcelToSO.cs, focus on changed](./.images/img1.2-4.jpg)

5. Run the code and check Console window for result.
   
   ![Console output focus on changed](./.images/img1.2-5.jpg)

6. Hide method "GetParent"
   
   Currently, the method StudentsData.GetParent is a public method.
   
   ![StudentsData.cs GetParent define](./.images/img1.2-6-1.jpg)
   
   The method GetParent can be successfully invoked.
   
   ![TestExcelToSO.cs GetParent](./.images/img1.2-6-2.jpg)
   
   Open "StudentsData.xlsx" and rename the sheet "Parent" into ".Parent". And then go back to Unity and execute "Process Excel" again.
   
   ![rename Parent into .Parent](./.images/img1.2-6-3.jpg)
   
   An compile error occurs ! "GetParent" method is invalid now.
   
   ![TestExcelToSO.cs GetParent is invalid](./.images/img1.2-6-4.jpg)
   
   After resolving the compile error, DO NOT forget to **re-execute "Process Excel"** to make the data asset file accurate.
