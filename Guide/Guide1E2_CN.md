# 使用数据类型嵌套

## 准备工作

完成[从创建新xlsx文件到在运行时读取其定义的数据](./Guide1E1_CN.md)中的内容

## 操作流程

1. 在StudentsData.xlsx表格文件中新增一个Sheet，将其重命名为Parent并填充数据。

   ![StudentsData.xlsx中的Parent表](./.images/img1.2-1.jpg)

2. 在Student表中新增两列，名为father和mother，其类型均为Parent，并为其填充数据，此列内容应与Parent表中的parent_id对应。

   ![Student表，突出新增两列](./.images/img1.2-2.jpg)

3. 回到Unity中并重新运行“Process Excel”。

   ![工具主界面，突出“Process Excel”按钮](./.images/img1.2-3.jpg)

4. 修改测试代码。

   ![TestExcelToSO.cs代码，突出新增](./.images/img1.2-4.jpg)

5. 运行测试，并查看控制台输出。

   ![控制台输出，突出新增](./.images/img1.2-5.jpg)

6. 隐藏GetParent方法。

   当前情况下，StudentsData中GetParent方法为public方法。

   ![StudentsData.cs中GetParent方法定义](./.images/img1.2-6-1.jpg)

   逻辑代码可以正常调用GetParent方法。

   ![TestExcelToSO.cs中调用GetParent](./.images/img1.2-6-2.jpg)

   回到StudentsData.xlsx中，将Parent表的名称改为“.Parent”，并回到Unity中重新运行“Process Excel”。

   ![Parent表重命名为“.Parent”](./.images/img1.2-6-3.jpg)

   此时出现了编译错误，GetParent方法无法调用。

   ![TestExcelToSO.cs中GetParent无法调用](./.images/img1.2-6-4.jpg)

   在解决完编译错误后**重新运行“Process Excel”**，以保证数据的正确。

   

