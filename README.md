# Excel To ScriptableObject

## Description

It's used to transform data in xlsx (which defines field name and field type) directly into Unity ScriptableObject asset. It's aimed at convenience in xlsx sheet output and fast retrieving sheet data at runtime.

Nested data type in one xlsx file between sheets is supported.

NO EXTRA API AT ALL. All you need to use will be in the generated codes。



## How It Works

1. A C-Sharp code will be generated in specific folder, the same file name with xlsx. All the data types in its sheets will be included, with the class names as sheets' names in xlsx file. The C-Sharp code is used for serializing and retrieving datas.
2. After compiling generated code, All the datas in sheets will be saved in a Unity asset in specific folder with the same file name with xlsx.
3. Before writing datas in asset file, datas in each sheets will be sorted, in order to make binary search available.



## Rules

- The file name of xlsx file will be used as class name and generated C-Sharp code's file name. So xlsx file name must follow the rules as class name does.
- All the sheets will be read if the sheet name does NOT start with '#'. The sheet name will be used as TYPE name, also class name. So sheet name must follow the rules as class name does.
- Before the first data row, at least 2 lines which indicate the field name and field type of their underlying column.
- No empty row is permitted between the first and last row.
- A folder besides Assets folder is recommended to store xlsx files.
- The row number of field names, field types and datas start should keep the SAME between xlsx files in ONE unity project.
- As to a specific xlsx file, the corresponding generated C-Sharp file, which represent data structure in the xlsx, its name space and other features could be modified independently.
- The first column of a  valid sheet must represent id or key, in order to index the line, also the item. And its type can only be int, long or string.
- It the data types are nested between sheets, the referenced sheet name, also type name, should be filled into the cell where type name lays. And the value of this field should be filled with the keys in referenced sheet.



## Work with It

[Guiding in Progress](./Guide/Guide0_EN.md)

1. Modify and check global configuration. Open "GreatClock->Excel2ScriptableObject" in menu bar to open management window to do this.
2. Create a xlsx file and fill the field types, field names and datas in sheets that you need.
3. Modify sheets and datas in xlsx anytime you need.
4. Go back to Unity，Open "GreatClock->Excel2ScriptableObject" in menu bar to open management window.
5. When adding new xlsx file, click "Add New Excel" or "Insert" to create new item. Then click "Select" to choose xlsx file to add. Specify which folders that generated C-Sharp code and asset file stores. Modify its name space, and other features.
6. Click "Process Excel" or "Process All" to generate or update C-Sharp code and write datas to serialized unity asset file for specific xlsx file(s) managed in management window.
7. Load data asset file just as loading other assets such as prefab, AudioClip, AnimationClip etc. You can drag data asset file to serialized field in MonoBehaviour, or load it by Resources.Load / AssetBundle。
8. Get the instance with the data type of the same name with xlsx file from loaded asset.
9. Call specific Get Method with id or key to retrieve data item(s) of specific sheet.
10. Debug.Log data item, or read the values of the instance by reading its properties.
11. If multi-language, 'lang' type, is used in xlsx file, a public variable 'System.Func<string, string> Translate' will be existed in generated C-Sharp file. By implementing this method and set your method to 'Translate' variable, you may transform the corresponding string into any string you need.
12. If rich string, 'rich' type, is used in xlsx file, a public variable 'Enrich' will be existed in generated C-Sharp code. It is similar to multi-language feature.



## Supported Base Data Types

### Boolean

Identifier : bool

Value Example : 

- true
- false
- yes
- no
- 1

### 32bit Integer

Identifier : int, int32

Value Example : 

- 0
- 12345
- -321

### 32bit Integer Array

Identifier : ints, int[], [int], int32s, int32[], [int32]

Value Example : 

- 123,234（NOT recommended）
- [321,-432,0]（recommended）

### 64bit Integer

Identifier : long, int64

Value Example : 

- 0
- 12345
- -321

### 64bit Integer Array

Identifier : longs, long[], [long], int64s, int64[], [int64]

Value Example : 

- 123,234 (NOT recommended)
- [321,-432,0] (recommended)

### Float

Identifier : float

Value Example : 

- 123
- 3.14
- -1.414
- 1E-5

### Float Array

Identifier : floats, float[], [float]

Value Example : 

- 3.14 (single element, NOT recommended)
- [3.14] (single element, recommended)

- 123,3.14 (NOT recommended)
- [123,3.14,-1.414] (recommended)

### Vector2

Identifier : vector2

Value Example : 

- x,y (format, NOT recommended)
- [x,y] (format, recommended)

### Vector3

Identifier : vector3

Value Example : 

- x,y,z (format, NOT recommended)
- [x,y,z] (format, recommended)

### Vector4

Identifier : vector4

Value Example : 

- x,y,z,w (format, NOT recommended)
- [x,y,z,w] (format, recommended)

### Rectangle

Identifier : rect, rectangle

Value Example : 

- x,y,w,h (format, NOT recommended, w : width, h : height)
- [x,y,w,h] (format, recommended)

### Color

Identifier :  color

Value Example : 

- r,g,b,a (format NOT recommended, rgba : integer between 0 - 255)

- r,g,b (format, NOT recommended)

- [r,g,b,a] (format, recommended)

- [r,g,b] (format, recommended)

- RRGGBBAA (format,recommended) e.g. FF000080(semi-transparent red), 808080FF(opaque gray)

- RRGGBB (format, NOT recommended, opaque color) e.g. FFFF00(yellow), 000000(black)

### String

Identifier : string

Value Example : 

- ABCDEFG HIJKLMN\nOPQ RST UVW XYZ

### String Array

Identifier : strings, string[], [string]

Value Example : 

- Hello World ! (single element)

- ABCDEFG,HIJKLMN,OPQ,RST,UVW,XYZ
- [AA,BB,CC,DD]

### Multi-Language Key

Identifier : lang, language

Value Example : 

- language_key

### Multi-Language Key Array

Identifier : langs, lang[], [lang], languages, language[], [language]

Value Example : 

- language_key (single element)

- language_key1,language_key2,language_key3
- [language_key1,language_key2,language_key3]

### Rich String Key

Identifier : rich

Value Example : 

- rich_key

### Rich String Key Array

Identifier : richs, riches, rich[], [rich]

Value Example : 

- rich_key (single element)

- rich_key1,rich_key2,rich_key3
- [rich_key1,rich_key2,rich_key3]

**NOTE :**

- All identifiers representing array are in format types, type[] or [type].
- A comma is used to separate elements when necessary.
- When multi elements is needed, square brackets are recommended in case of number format issue in Excel.



## Other Features

- **Use Hash String** : Collect all strings in the xlsx file and store them in a string array, in order to optimize asset storage when multiple duplicated strings exist in xlsx file.
- **Hide Asset Properties** : If NOT checked, all serialized properties and their values will be shown in Inspector window when data asset file is selected in Project window. If checked, all the serialized properties will be hidden to prevent from being modified in Unity Editor.
- **Public Items Getter** : If checked, a method will be generated to retrieve all data in each sheet.
- **Compress Color into Integer** : Compress color into 32 bit integer, in order to optimize color storage. You'll always get a UnityEngine.Color when you retrieve value of a color property.
- **Treat Unknown Types as Enum** : If checked, you can define a enum type in sheets. If a enum type is necessary, just fill the type field with enum type you need to define, and fill its data column with the enum values that needed. The enum type in generated C-Sharp code will exactly contains all the enum values that exists in your columns with the enum type.
- **Generate ToString Meghod** : If checked, a ToString method will be generated to override its base. It's easy to make your data item readable when Debug Log to console.
- In xlsx file, sheets with name that starts with '#' will be ignored.
- In xlsx file, sheets with name that starts with '.' will be treated as internal datas that cannot be retrieved externally by calling Get method.
- Sheets with the name matches {SheetName}, [SheetName], SheetName{}, SheetName[] will be treated as key-to-multi-value sheet. By calling corresponding Get method, you'll get all the items that have the specific id or key.
- In xlsx file, if the key of a data item of a valid sheet is empty or '-', the item will not be serialized. But if the item contains an enum value, the enum value will be used. If the item contains an id of nested sheet, then the nested sheet item will be mark as used.
- In management window, if an Excel item has its xlsx file specified, then the 'Open' button is available. By clicking this button, the xlsx file will be opened. By shift-holding and clicking the button, the folder that contains the xlsx file will be shown in explorer.
- Global configurations and managed xlsxs with their configurations will be stored in 'ProjectSettings/ExcelToScriptableObjectSettings.asset' in json format. It's easy for version control, and manually editing is OK. When editing manually, make sure it keeps the accurate json format.



## FAQs

**Q:** How to deal with the compile error because of duplicated Excel.dll, ICSharpCode.SharpZipLib.dll or System.Data.dll when this plugin-tool is imported.

**A:** All the three dynamic link libraries are common libraries. If duplicated dll importing occurred, keep only one of the same dlls in your project will make it work. All the three dlls used by this plugin-tool are only used in Unity Editor. Take care of dll import settings when necessary.

------

**Q:** How to deal with compile errors after clicking "Process Excel" ?

**A:** To make sure the data asset file is accurate, "Process Excel" should execute again after compile errors are resolved. Any of the following conditions may bring a compile error:

- Unqualified xlsx file name, sheet name or field name. Fix : Make the name C-Sharp qualified to class name or field name. Remember to remove deprecated previous generated C-Sharp code and asset file.
- Some code in your project is referencing a field that has been renamed or removed in xlsx file. Fix : the previous field is no longer available. You should modify your logic in your code that referencing the deprecated field.
- Duplicated generated C-Sharp codes because of changing code storage directory. Fix : Remove the C-Sharp file in your previous code storage directory.
- Classes in generated C-Sharp code are of the same names with your logic classes. Fix : Rename your xlsx file name or sheet name or your logic class name that involved. Or set a separated name space to your generated C-Sharp code.

------

**Q:** When using multi-language or rich-text, their values are not reset after calling Reset() method of data asset containing the data item.

**A:** The real values of multi-language or rich-text field are set only when the data item is retrieved. This is designed to avoid mass data process when there are massive data items. It's recommended NOT to keep the data item for long time. Retrieving data item again after Reset() is called is also a good choice.

------

**Q:** My Data types are nested, and the field type referencing another type is not mark as an array. But why the field is an array in generated C-Sharp code?

**A:** When data types are nested, either the field type is marked as array or the referenced data type is key-to-multi-values will make the field referencing another type be an array.