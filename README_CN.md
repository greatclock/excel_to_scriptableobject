# Excel To ScriptableObject

[README.md in English](./README.md)

## 概述

用于将定义了数据字段名称和字段类型的Excel表格（.xlsx）文件直接导出成Unity资源，以方便配置数据的导出和游戏中对模板表类的配置数据的快速读取。

支持同一Excel文件中多个sheet之间数据类型的嵌套。

整个方案无任何API，所有需要程序代码调用的内容，都在生成的数据代码中。

## 内部原理

1. 根据Excel表中的字段名称和类型，在指定的目录中生成用于序列化资源的cs代码，代码中包含与Excel文件同名的用于序列化资源及运行时读取数据的类，以及与sheet同名的描述其数据类型的类。

2. 在编译完成后，读取Excel文件中所有数据，并写入指定文件夹的同名资源中。

3. 在将数据写入资源文件之前，先将Excel中读取到的数据进行排序，以方便用二分查找快速读取到指定的条目。

## 规则

- Excel文件的文件名将作为类名及生成的cs文件的文件名使用，其命名规则与类名的命名规则相同。

- Excel文件中所有非名称以#开头的sheet，其数据都将被读取，其名称将被作为数据结构的类型名称，故其命名规则与类名的规则相同。

- 在数据开始的行的上面，至少应有两行用于标识所在列的数据字段名称和数据类型。

- Excel文件建议放在工程中与Assets文件夹同级别的文件夹中。

- Excel文件中哪一行用于定义字段名称，哪一行用于定义字段类型，以及数据从哪一行起，三个参数为全局参数，在一个项目中应保持一致。

- 每个Excel文件对应的数据结构文件（C#脚本）及其命名空间等相关参数可单独指定。

- 数据sheet的第一列必须表示id或key，用于索引该行（条目）数据，其数据类型只能为int、long或string。

- 若使用sheet间数据类型的嵌套，则数据类型处应填写对应的sheet名称，在数据中填写对应sheet中指定条目的键。

## 操作步骤

[使用示例](./Guide/Guide0_CN.md)

1. 制定、检查全局设置，在Unity菜单栏中点击GreatClock->Excel2ScriptableObject->Open Window，打开管理窗口进行操作。

2. 创建Excel（.xlsx）文件并根据需求定制sheet及其中的字段名称和类型。

3. 填充或修改表中的数据。

4. 打开Unity，在菜单栏中点击GreatClock->Excel2ScriptableObject->OpenWindow，打开管理窗口。

5. 若为新增Excel文件，选择Add New Excel Setting或通过点击右下角加号按钮增加条目，点击Select选择该Excel文件，并指定其对应的C#文件、Unity数据资源的存放目录、代码命名空间等相关参数。

6. 通过Process Excel或Process All将指定的Excel文件或窗口中管理的所有Excel文件分析生成代码并序列化成Unity资源（.asset）文件。

7. 运行时加载Excel文件对应的Unity资源。加载方法同其他Unity资源（如prefab、AudioClip、AnimationClip等）无异，如MonoBehaviour中[SerializeField]属性+拖拽、Resources.Load或AssetBundle等。

8. 从加载到的资源中获取与Excel文件同名的类的实例。

9. 调用该资源实例中对应的Get方法并传入id或key参数为键以获取指定sheet中对应的条目数据。

10. 直接打印返回的条目数据，或通过读取变量数据的方式读取该条目的所有字段的数据。

11. 若Excel文件中使用了多语言字段类型，则在与Excel文件同名的类的实例中，会有System.Func\<string, string\> Translate函数变量。通过自定义此变量，即可实现自定义内容的转换。为保证正确，请在读取数据前先设置好此变量。

12. 若Excel文件中使用了内容富文本字段类型，与多语言字段类型类似，其自定义的函数变量为Enrich。

## 支持的数据类型

### 布尔值

标识符：bool

表示方法：

- true

- false

- yes

- no

- 1

### 短整型数

标识符：int、int32

表示方法：

- 0

- 12345

- -321

### 短整形数数组

标识符：ints、int[]、[int]、int32s、int32[]、[int32]

表示方法：

- 123,234（不推荐）

- [321,-432,0]（推荐）

### 长整型数

标识符：long、int64

表示方法：

- 0
- 12345
- -321

### 长整形数数组

标识符：longs、long[]、[long]、int64s、int64[]、[int64]

表示方法：

- 123,234（不推荐）

- [321,-432,0]（推荐）

### 浮点数

标识符：float

表示方法：

- 123

- 3.14

- -1.414

- 1E-5

### 浮点数数组

标识符：floats、float[]、[float]

表示方法：

- 3.14（一个元素，不推荐）

- [3.14]（一个元素，推荐）

- 123,3.14（不推荐）

- [123,3.14,-1.414]（推荐）

### 二维向量

标识符：vector2

表示方法：

- x,y（格式，不推荐）

- [x,y]（格式，推荐）

### 三维向量

标识符：vector3

表示方法：

- x,y,z（格式，不推荐）

- [x,y,z]（格式，推荐）

### 四维向量

标识符：vector4

表示方法：

- x,y,z,w（格式，不推荐）

- [x,y,z,w]（格式，推荐）

### 矩形

标识符：rect、rectangle

表示方法：

- x,y,w,h（格式，不推荐，w：宽，h：高）

- [x,y,w,h]（格式，推荐）

### 颜色

标识符： color、colour

表示方法：

- r,g,b,a（格式，不推荐，rgba为0-255的整数）

- r,g,b（格式，不推荐）

- [r,g,b,a]（格式，推荐）

- [r,g,b]（格式，推荐）

- RRGGBBAA（格式，推荐），例：FF000080（半透明的纯红色）、808080FF（不透明的灰色）

- RRGGBB（格式，推荐，不透明颜色），例：FFFF00（黄色）、000000（黑色）

### 字符串

标识符：string

表示方法：

- ABCDEFG HIJKLMN\nOPQ RST UVW XYZ

### 字符串数组

标识符：strings、string[]、[string]

表示方法：

- Hello World !（一个元素）

- ABCDEFG,HIJKLMN,OPQ,RST,UVW,XYZ

- [AA,BB,CC,DD]

### 多语言键

标识符：lang、language

表示方法：

- language_key

### 多语言键数组

标识符：langs、lang[]、[lang]、languages、language[]、[language]

表示方法：

- language_key（一个元素）

- language_key1,language_key2,language_key3

- [language_key1,language_key2,language_key3]

### 内容富文本键

标识符：rich

表示方法：

- rich_key

### 内容富文本键数组

标识符：richs、riches、rich[]、[rich]

表示方法：

- rich_key（一个元素）

- rich_key1,rich_key2,rich_key3

- [rich_key1,rich_key2,rich_key3]

**注：**

- 标识符中表示数组的均为types、type[]或[type]

- 所有需要定义多个元素的数据类型，其元素间的分隔符均为半角逗号

- 所有需要定义多个数据的数据类型在表示数据时，推荐使用半角中括号以避免Excel中数字格式问题

## 其他选项及功能

- **Use Hash String** ：对于一个Excel文件中所有的字符串进行统一存储，以避免因内容大量重复造成的存储空间上的浪费。

- **Hide Asset Properties** ：若未选中此项，在Unity Project窗口选中数据文件时，Inspector窗口中将显示其所有序列化信息。选中此荐后，将对这些信息进行隐藏，以避免数据在Unity编辑器中被错误修改。

- **Public Items Getter** ：是否生成获取sheet中所有条目的方法。

- **Compress Color into Integer** ：把颜色数据压缩成整形数存储。用于优化数据文件的存储空间，并不影响颜色的定义方式和读取结果（读取时得到的数据的类型始终为UnityEngine.Color）。

- **Treat Unknown Types as Enum** ：将未知类型的数据按枚举类型处理，方便在Excel文件中定义枚举类型的数据。若某一字段需要使用枚举值，则需要在表中字段类型定义处填入需要的枚举类型名称，在数据条目中的该列处，填写条目所需要的枚举值即可。代码中所有枚举值为该类型的枚举的列中出现的所有的不同的值。

- **Generate ToString Meghod** ：指定是否生成ToString方法。推荐勾选此荐，尤其是在调试过程中，以方便直接通过控制台打印日志的方式阅读条目中的所有数据。

- Excel文件中，名称以#开头的sheet将不被读取。

- Excel文件中，名称以.开头的sheet将不暴露其Get方法，其中仅被其他sheet使用的数据才会被读取。

- 名称以{SheetName}、[SheetName]、SheetName{}、SheetName[]为形式的sheet，认为该sheet中一个id或key对应多个条目，此时Get方法将返回（或写入）一个包含了所有指定id或key的列表。

- Excel文件的有效sheet中，若条目的第一列（即id或key）的值若为空或"-"或以"#"开头，该条目将不被读取，但其中的枚举值或其他sheet中数据类型的key（若包含）将认为被使用，以实现占位用途。

- 支持在工程代码中的枚举类型，在表格中的数据类型处，应填写完整的枚举类型名称，包含命名空间及枚举的类型名，并需要在表格中使用枚举值的名称作为该字段的值。枚举类型应直接定义在命名空间中，不可使用在类中定义的枚举类型。

- 在管理窗口中，对于指定的条目，若已经指定了Excel文件，Open按钮可用，点击即可直接打开该Excel文件；若按住shift点击Open按钮，则会打开该Excel文件所在的文件夹。

- 在管理窗口中，双击Script Directory或Asset Directory文本，工程中对应的代码或保存着数据的资源会高亮，如果代码或保存着数据的资源不存在，则其文件夹将会高亮。

- 全局设置和管理的所有Excel文件及其配置都以json文本方式存储在ProjectSettings/ExcelToScriptableObjectSettings.asset文件中，方便版本管理和手动修改。在手动修改时，请保证json格式正确。

- 支持在Editor代码中定义规则，以筛选指定表中的字段、修改生成的代码及资源中实际使用的字段名。

## 常见问题

**问：** 关于Excel.dll、ICSharpCode.SharpZipLib.dll或System.Data.dll重复而导致的导入此工具插件后编译错误。

**答：** 此三个dll均为常见的标准的动态链接库，若有因重复导入而导致编译错误的情况，同一个dll保留一个即可。此工具插件中对这三个dll的引用均在Unity Editor之中，请注意dll文件的导入设置。

------

**问：** Process Excel后产生编译错误如何处理？

**答：** 产生编译错误后，无法保证其数据资源的准确，需要解决编译错误后重新Process Excel。产生编译错误的情况会有很多，如：

- Excel文件名、sheet名称和字段名称不符合命名规范：修改名称，若Excel文件名修改，记得删除上次生成的代及资源文件。

- 项目代码中引用了在Excel文件中被修改了名称或删除了的字段：此时原字段已无法使用，需要修改此字段删改对应的逻辑代码。

- 由于生成代码的目录修改，同一Excel文件对应的代码存在两份：删除原代码生成目录中的对应代码即可。

- 逻辑代码中存在同名类：修改重名的Excel文件名或sheet名称或逻辑代码中同名的类的名称，或使用命名空间进行区分。

------

**问：** 调用资源实例的Reset该当后，已读取的该资源中包含的条目的多语言或富文本字段值并没有改变。

**答：** 多语言和富文本字段的值，是在获取到该数据条目时才进行赋值操作，以避免存在大量数据时，一次性操作而导致的卡顿。建议不要对读取到的数据条目进行长时间存储，或在调用Reset后，重新获取受影响的数据条目。

------

**问：** 在使用嵌套类型时，为什么被引用类型的字段类型未标记为数组，但生成的代码中该字段却为数组？

**答：** 使用嵌套时，被引用的类型的字段为数组的条件为：该字段被标识为数组，被引用的类型对应的sheet被标记为一键对应多值。