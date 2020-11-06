# Marking a sheet as "key to multi values" and reading all items that match

## We Assume You've Finished

Finished the guidance [Using Enum types](./Guide1E3_EN.md)

## Demands in Guiding

In this guide, we are going to group students in "Student" sheet in "StudentsData.xlsx", and define a type for group member.

Properties of group member : member information, job in group。

We are going to get all group members in group with group id using the feature "key to multi values".

## Guiding in Progress

1. Create a new sheet named {GroupMember}, GroupMember{}, [GroupMember] or GroupMember[] in StudentsData.xlsx.

   **Note :** the braces "{}" or square brackets "[]" are used to mark that data items in the sheet will be "key to multi values". In some xlsx applications square brackets are invalid in sheet names, so in this tool, braces works the same as square brackets when dealing with sheet names. In this guide, our sheet name for group members will be "{GroupMember}".

2. Fill the sheet "{GroupMember}" with proper data types and contents.

   ![mark sheet as key to multi values](./.images/img2.1-1.jpg)

3. Go back to Unity and execute "Process Excel" again.

4. Append the following code into Start method of "TestExcelToSO.cs".

   ![get all group member in group group_id == 199102, and print them](./.images/img2.1-2.jpg)

5. Run the code and check Console window for result.

   ![Console output, focus on job property and three members](./.images/img2.1-3.jpg)

