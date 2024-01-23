# Guiding in Progress

## Import package into Unity project using Git repository

**Note :** Using Git repository is not the only way to import this package into your project. You can also download the code and dlls into your project, or import the package from your remote npm where this package is deployed.

**Option 1 (all versions of Unity that supports Package Manager is OK) :**

1. Open Packages/manifest.json in your project with any text editor

2. Insert a new line as below into "dependencies"
   
   ```json
   {
     "dependencies": {
       "com.greatclock.exceltoscriptableobject": "git+https://github.com/greatclock/excel_to_scriptableobject.git",
       ...
     }
   }
   ```

3. Save the file Packages/manifest.json

4. Back to Unity, and the package will be imported automatically

**Option 2 (Unity2019 or newer only) :**

1. Open "Package Manager" and click the "+" button on the left top. And then select "Add package from git URL..."
   
   ![](./.images/img0.1.jpg)

2. Fill in the blank with "https://github.com/greatclock/excel_to_scriptableobject.git" and then click "Add" button
   
   ![](./.images/img0.2.jpg)

A folder named "Excel To ScriptableObject" will be presented in Packages of Project window after the package imported into Unity.

![](./.images/img0.3.jpg)

## Contents in Guiding

### 1. Basic Usages

In this guiding, we are going to make a ".xlsx" file that contains datas a bunch of students, including their basic information such as name, gender, and their parents' information with their phone number.

It aimed to show how basic data types, enum types, arrays and nested types are used in this tool.

#### 1.1 Reading data in Unity which fulfilled in a new xlsx file

[Guide1E1_EN.md](./Guide1E1_EN.md)

- The whole work flow
- Rules in all useful names
- Comments row in xlsx files
- Using basic data types and arrays
- Retrieving data in Unity and debugging data

#### 1.2 Using nested types

[Guide1E2_EN.md](./Guide1E2_EN.md)

- Multi sheets in one xlsx file
- Nesting data in sheets via id or key
- Hiding get method of a specific sheet to make it internally used only

#### 1.3 Using Enum types

[Guide1E3_EN.md](./Guide1E3_EN.md)

- Procedures in using enum types
- Defining enum values for enum types in sheet

### 2. Key to Multi Values

#### 2.1 Marking a sheet as "key to multi values" and reading all items that match

[Guide2E1_EN.md](./Guide2E1_EN.md)

- Dealing with special characters in various xlsx applications
- Avoiding GC alloc caused by creating 'List' when retrieving items

#### 2.2 When using nested types, the field defined as a custom type can be an array

[Guide2E2_EN.md](./Guide2E2_EN.md)

### 3. Extensions for String

#### 3.1 Multi-Language feature

[Guide3E1_EN.md](./Guide3E1_EN.md)

- Using multi-language string
- Using registered key-value translation function to make multi-language field available with nothing else to do

#### 3.2 Logical Rich-Text feature

**Note :** Logical Rich-Text means : one or more segment of a string may be replaced with contents only of value at the time that the string shows. For example : player's nick name, player's current level. It's NOT used for dealing with text colors, sizes or in-text images.

[Guide3E2_EN.md](./Guide3E2_EN.md)

- Using logical rich-text string
- Using registered enrich function to make logical rich-text field available with nothing else to do

### 4. One Data Script to Multi .xlsx Files

#### Possible Usages

- Grouping users, AB test : Load different groups of data assets according to users' properties. The types of data assets in each group should be consistent.

- Multi-Language : Load different data assets containing specific language text according to user's current language. Thus no extra languages text will be loaded, and it's possible to switch between languages.

- Offline update : The client should download the data assets that will be used beforehand. And the latest asset can be used after a specific time point, even if the user is offline.

#### Operation Steps

1. Click "+" button at right-bottom of `Slave Excels` in Excel Setting. Thus a slave item for a .xlsx file is added.

2. Click "Select" to select a .xlsx file and specify a folder where the data asset lays.

3. Click `Flush Data` to generate data asset for the selected .xlsx file.

#### Notice

- No script or data type will be generated for .xlsx file in `Slave Excels`. The generated asset file for slave excel will be of the same type of  the `Excel Setting` it belongs to.

- The sheets and fileds in slave .xlsx files should keep consistent with the .xlsx file in `Excel Setting`.

### 5. Filtering Fields in Sheet

Some possible rules for a project :

- The first 2 rows in all .xlsx sheets represent field names and field types. The 3rd row is specifies if the field is used for Client only (C) or Server only (S) or Both (CS).

- If the field is used for Server only, the field together with data in it will not be imported into client.

The following codes describes foregoing rules :

```csharp
// Assets/Editor/ExcelFieldFilterDemo.cs
using GreatClock.Common.ExcelToSO;
public class ExcelFieldFilterDemo {
    // Match all tables via regex, with priority 0, reading the 3rd (index:2) row of the field for filtering.
    [ExcelFieldFilter(eMatchType.Regex, @".+", priority: 0, requireRowIndex: 2)]
    // Filter method must be static.
    // The first two parameters will be table name and field name.
    // The returned value is the field name that will be used in code and data asset.
    // If null or empty string is returned, the field will be ignored.
    static string DefaultFieldFilter(string tablename, string fieldname, string content) {
        if (!string.IsNullOrEmpty(content) && (content.Contains("c") || content.Contains("C"))) {
            return fieldname;
        }
        return null;
    }
}
```

Notice :

- The first column in each sheet will used as table index, and will not be filtered.

- One table (aka sheet) may match more than one field filters. But only the one with biggest priority will be in use.

- `requireRowIndex` is a optional parameter. If it's not specified, the `content` parameter in field filter method should be removed.

- The field filter method returns the field name that will be used in code and data asset. So you can use field filter method to fix the field name in .xlsx file.

- More than one `ExcelFieldFilter` can be added to a field filter method.
