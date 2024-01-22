using Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GreatClock.Common.ExcelToSO {

	public class ExcelToScriptableObject : EditorWindow {

		public const string SETTINGS_PATH = "ProjectSettings/ExcelToScriptableObjectSettings.asset";

		private static Regex reg_color32 = new Regex(@"^[A-Fa-f0-9]{8}$");
		private static Regex reg_color24 = new Regex(@"^[A-Fa-f0-9]{6}$");

		[MenuItem("GreatClock/Excel To ScriptableObject/Open Window")]
		static void Excel2ScriptableObject() {
			ExcelToScriptableObject window = GetWindow<ExcelToScriptableObject>("Process Excel");
			window.minSize = new Vector2(540f, 320f);
			window.maxSize = new Vector2(4000f, 4000f);
		}

		[MenuItem("GreatClock/Excel To ScriptableObject/Process All")]
		public static void ProcessAll() {
			ReadsSettings();
			for (int i = 0, imax = excel_settings.Count; i < imax; i++) {
				ExcelToScriptableObjectSetting excel_setting = excel_settings[i];
				if (!CheckProcessable(excel_setting)) { continue; }
				FlushDataSettings setting = GetFlushDataSettings(excel_setting);
				FlushData(setting);
				for (int j = 0, jmax = excel_setting.slaves.Length; j < jmax; j++) {
					var slave = excel_setting.slaves[j];
					if (!string.IsNullOrEmpty(slave.excel_name) && CheckIsDirectoryValid(slave.asset_directory)) {
						setting.excel_path = slave.excel_name;
						setting.asset_directory = slave.asset_directory;
						FlushData(setting);
					}
				}
			}
			AssetDatabase.SaveAssets();
		}

		private class SheetData {
			public DataTable table;
			public string itemClassName;
			public bool keyToMultiValues;
			public bool internalData;
			public List<FieldData> fields = new List<FieldData>();
			public List<int> indices = new List<int>();
		}

		private class FieldData {
			public string fieldName;
			public int fieldIndex;
			public eFieldTypes fieldType;
			public string fieldTypeName;
		}

		private struct GenerateCodeSettings {
			public string excel_path;
			public string script_directory;
			public string name_space;
			public bool use_hash_string;
			public bool hide_asset_properties;
			public bool use_public_items_getter;
			public bool compress_color_into_int;
			public bool treat_unknown_types_as_enum;
			public bool generate_tostring_method;
		}

		[Serializable]
		private struct FlushDataSettings {
			public string excel_path;
			public string asset_directory;
			public string class_name;
			public bool use_hash_string;
			public bool compress_color_into_int;
			public bool treat_unknown_types_as_enum;
		}

		static bool GenerateCode(GenerateCodeSettings settings) {
			List<SheetData> sheets = new List<SheetData>();
			Dictionary<string, List<string>> unknownTypes = new Dictionary<string, List<string>>();
			Dictionary<string, List<string>> customTypes = new Dictionary<string, List<string>>();
			string className;
			bool hasLang;
			bool hasRich;
			if (!ReadExcel(settings.excel_path, settings.treat_unknown_types_as_enum, sheets, unknownTypes, customTypes, out className, out hasLang, out hasRich)) { return false; }

			string serializeAttribute = settings.hide_asset_properties ? "[SerializeField, HideInInspector]" : "[SerializeField]";
			StringBuilder content = new StringBuilder();
			content.AppendLine("//----------------------------------------------");
			content.AppendLine("//    Auto Generated. DO NOT edit manually!");
			content.AppendLine("//----------------------------------------------");
			content.AppendLine();
			content.AppendLine("#pragma warning disable 649");
			content.AppendLine();
			content.AppendLine("using System;");
			content.AppendLine("using UnityEngine;");
			bool usingCollections = false;
			if (settings.use_public_items_getter) {
				usingCollections = true;
			} else {
				foreach (SheetData sheet in sheets) {
					if (sheet.keyToMultiValues) {
						usingCollections = true;
						break;
					}
				}
			}
			if (usingCollections) { content.AppendLine("using System.Collections.Generic;"); }
			content.AppendLine();

			string indent = "";
			if (!string.IsNullOrEmpty(settings.name_space)) {
				content.AppendLine(string.Format("namespace {0} {{", settings.name_space));
				content.AppendLine();
				indent = "\t";
			}

			content.AppendLine(string.Format("{0}public partial class {1} : ScriptableObject {{", indent, className));
			content.AppendLine();
			if (settings.use_hash_string) {
				content.AppendLine(string.Format("{0}\t{1}", indent, serializeAttribute));
				content.AppendLine(string.Format("{0}\tprivate string[] _HashStrings;", indent));
				content.AppendLine();
			}
			foreach (KeyValuePair<string, List<string>> kv in unknownTypes) {
				content.AppendLine(string.Format("{0}\tpublic enum {1} {{", indent, kv.Key));
				content.Append(indent);
				content.Append("\t\t");
				bool firstEnum = true;
				for (int i = 0, imax = kv.Value.Count; i < imax; i++) {
					string ev = kv.Value[i].Trim();
					if (string.IsNullOrEmpty(ev)) { continue; }
					if (!firstEnum) { content.Append(", "); }
					firstEnum = false;
					content.Append(ev);
				}
				content.AppendLine();
				content.AppendLine(string.Format("{0}\t}}", indent));
				content.AppendLine();
			}
			if (hasLang) {
				content.AppendLine(string.Format("{0}\tpublic Func<string, string> Translate;", indent));
				content.AppendLine();
			}
			if (hasRich) {
				content.AppendLine(string.Format("{0}\tpublic Func<string, string> Enrich;", indent));
				content.AppendLine();
			}
			content.AppendLine(string.Format("{0}\t[NonSerialized]", indent));
			content.AppendLine(string.Format("{0}\tprivate int mVersion = 1;", indent));
			content.AppendLine();
			List<int> customTypeIndices = new List<int>();
			foreach (SheetData sheet in sheets) {
				content.AppendLine(string.Format("{0}\t{1}", indent, serializeAttribute));
				content.AppendLine(string.Format("{0}\tprivate {1}[] _{1}Items;", indent, sheet.itemClassName));
				if (settings.use_public_items_getter) {
					content.AppendLine(string.Format("{0}\tpublic int Get{1}Items(List<{1}> items) {{", indent, sheet.itemClassName));
					content.AppendLine(string.Format("{0}\t\tint len = _{1}Items.Length;", indent, sheet.itemClassName));
					content.AppendLine(string.Format("{0}\t\tfor (int i = 0; i < len; i++) {{", indent));
					content.AppendLine(string.Format("{0}\t\t\titems.Add(_{1}Items[i].Init(mVersion, DataGetterObject));",
						indent, sheet.itemClassName));
					content.AppendLine(string.Format("{0}\t\t}}", indent));
					content.AppendLine(string.Format("{0}\t\treturn len;", indent));
					content.AppendLine(string.Format("{0}\t}}", indent));
				}
				content.AppendLine();
				FieldData firstField = sheet.fields[0];
				string idVarName = firstField.fieldName;
				idVarName = idVarName.Substring(0, 1).ToLower() + idVarName.Substring(1, idVarName.Length - 1);
				bool hashStringKey = firstField.fieldType == eFieldTypes.String && settings.use_hash_string;
				if (sheet.keyToMultiValues) {
					if (!sheet.internalData) {
						content.AppendLine(string.Format("{0}\tpublic List<{1}> Get{1}List({2} {3}) {{", indent, sheet.itemClassName,
							GetFieldTypeName(firstField.fieldType), idVarName));
						content.AppendLine(string.Format("{0}\t\tList<{1}> list = new List<{1}>(); ", indent, sheet.itemClassName));
						content.AppendLine(string.Format("{0}\t\tGet{1}List({2}, list);", indent, sheet.itemClassName, idVarName));
						content.AppendLine(string.Format("{0}\t\treturn list;", indent));
						content.AppendLine(string.Format("{0}\t}}", indent));
					}
					content.AppendLine(string.Format("{0}\t{1} int Get{2}List({3} {4}, List<{2}> list) {{", indent,
						sheet.internalData ? "private" : "public", sheet.itemClassName,
						GetFieldTypeName(firstField.fieldType), idVarName));
					content.AppendLine(string.Format("{0}\t\tint min = 0;", indent));
					content.AppendLine(string.Format("{0}\t\tint len = _{1}Items.Length;", indent, sheet.itemClassName));
					content.AppendLine(string.Format("{0}\t\tint max = len;", indent));
					content.AppendLine(string.Format("{0}\t\tint index = -1;", indent));
					content.AppendLine(string.Format("{0}\t\twhile (min < max) {{", indent));
					content.AppendLine(string.Format("{0}\t\t\tint i = (min + max) >> 1;", indent));
					content.AppendLine(string.Format("{0}\t\t\t{1} item = _{1}Items[i]{2};", indent, sheet.itemClassName,
						hashStringKey ? ".Init(mVersion, DataGetterObject, true)" : ""));
					content.AppendLine(string.Format("{0}\t\t\tif (item.{1} == {2}) {{", indent, firstField.fieldName, idVarName));
					content.AppendLine(string.Format("{0}\t\t\t\tindex = i;", indent));
					content.AppendLine(string.Format("{0}\t\t\t\tbreak;", indent));
					content.AppendLine(string.Format("{0}\t\t\t}}", indent));
					if (firstField.fieldType == eFieldTypes.String) {
						content.AppendLine(string.Format("{0}\t\t\tif (string.Compare({1}, item.{2}) < 0) {{", indent, idVarName, firstField.fieldName));
					} else {
						content.AppendLine(string.Format("{0}\t\t\tif ({1} < item.{2}) {{", indent, idVarName, firstField.fieldName));
					}
					content.AppendLine(string.Format("{0}\t\t\t\tmax = i;", indent));
					content.AppendLine(string.Format("{0}\t\t\t}} else {{", indent));
					content.AppendLine(string.Format("{0}\t\t\t\tmin = i + 1;", indent));
					content.AppendLine(string.Format("{0}\t\t\t}}", indent));
					content.AppendLine(string.Format("{0}\t\t}}", indent));
					content.AppendLine(string.Format("{0}\t\tif (index < 0) {{ return 0; }}", indent));
					content.AppendLine(string.Format("{0}\t\tint l = index;", indent));
					content.AppendLine(string.Format("{0}\t\twhile (l - 1 >= 0 && _{1}Items[l - 1].{2} == {3}) {{ l--; }}",
						indent, sheet.itemClassName, firstField.fieldName, idVarName));
					content.AppendLine(string.Format("{0}\t\tint r = index;", indent));
					content.AppendLine(string.Format("{0}\t\twhile (r + 1 < len && _{1}Items[r + 1].{2} == {3}) {{ r++; }}",
						indent, sheet.itemClassName, firstField.fieldName, idVarName));
					content.AppendLine(string.Format("{0}\t\tfor (int i = l; i <= r; i++) {{", indent));
					content.AppendLine(string.Format("{0}\t\t\tlist.Add(_{1}Items[i].Init(mVersion, DataGetterObject{2}));",
						indent, sheet.itemClassName, hashStringKey ? ", false" : ""));
					content.AppendLine(string.Format("{0}\t\t}}", indent));
					content.AppendLine(string.Format("{0}\t\treturn r - l + 1;", indent));
					content.AppendLine(string.Format("{0}\t}}", indent));
				} else {
					content.AppendLine(string.Format("{0}\t{1} {2} Get{2}({3} {4}) {{", indent,
						sheet.internalData ? "private" : "public", sheet.itemClassName,
						GetFieldTypeName(firstField.fieldType), idVarName));
					content.AppendLine(string.Format("{0}\t\tint min = 0;", indent));
					content.AppendLine(string.Format("{0}\t\tint max = _{1}Items.Length;", indent, sheet.itemClassName));
					content.AppendLine(string.Format("{0}\t\twhile (min < max) {{", indent));
					content.AppendLine(string.Format("{0}\t\t\tint index = (min + max) >> 1;", indent));
					if (hashStringKey) {
						content.AppendLine(string.Format("{0}\t\t\t{1} item = _{1}Items[index].Init(mVersion, DataGetterObject, true);",
							indent, sheet.itemClassName));
						content.AppendLine(string.Format("{0}\t\t\tif (item.{1} == {2}) {{ return item.Init(mVersion, DataGetterObject, false); }}",
							indent, firstField.fieldName, idVarName));
					} else {
						content.AppendLine(string.Format("{0}\t\t\t{1} item = _{1}Items[index];",
							indent, sheet.itemClassName));
						content.AppendLine(string.Format("{0}\t\t\tif (item.{1} == {2}) {{ return item.Init(mVersion, DataGetterObject); }}",
							indent, firstField.fieldName, idVarName));
					}
					if (firstField.fieldType == eFieldTypes.String) {
						content.AppendLine(string.Format("{0}\t\t\tif (string.Compare({1}, item.{2}) < 0) {{", indent, idVarName, firstField.fieldName));
					} else {
						content.AppendLine(string.Format("{0}\t\t\tif ({1} < item.{2}) {{", indent, idVarName, firstField.fieldName));
					}
					content.AppendLine(string.Format("{0}\t\t\t\tmax = index;", indent));
					content.AppendLine(string.Format("{0}\t\t\t}} else {{", indent));
					content.AppendLine(string.Format("{0}\t\t\t\tmin = index + 1;", indent));
					content.AppendLine(string.Format("{0}\t\t\t}}", indent));
					content.AppendLine(string.Format("{0}\t\t}}", indent));
					content.AppendLine(string.Format("{0}\t\treturn null;", indent));
					content.AppendLine(string.Format("{0}\t}}", indent));
				}
				content.AppendLine();
			}
			content.AppendLine(string.Format("{0}\tpublic void Reset() {{", indent));
			content.AppendLine(string.Format("{0}\t\tmVersion++;", indent));
			content.AppendLine(string.Format("{0}\t}}", indent));
			content.AppendLine();
			content.AppendLine(string.Format("{0}\tpublic interface IDataGetter {{", indent));
			if (settings.use_hash_string) {
				content.AppendLine(string.Format("{0}\t\tstring[] strings {{ get; }}", indent));
			}
			if (hasLang) {
				content.AppendLine(string.Format("{0}\t\tstring Translate(string key);", indent));
			}
			if (hasRich) {
				content.AppendLine(string.Format("{0}\t\tstring Enrich(string key);", indent));
			}
			foreach (SheetData sheet in sheets) {
				FieldData firstField = sheet.fields[0];
				string idVarName = firstField.fieldName;
				if (sheet.keyToMultiValues) {
					content.AppendLine(string.Format("{0}\t\tint Get{1}List({2} {3}, List<{1}> list);",
						indent, sheet.itemClassName, GetFieldTypeName(firstField.fieldType), idVarName));
				} else {
					content.AppendLine(string.Format("{0}\t\t{1} Get{1}({2} {3});",
						indent, sheet.itemClassName, GetFieldTypeName(firstField.fieldType), idVarName));
				}
			}
			content.AppendLine(string.Format("{0}\t}}", indent));
			content.AppendLine();
			content.AppendLine(string.Format("{0}\tprivate class DataGetter : IDataGetter {{", indent));
			if (settings.use_hash_string) {
				content.AppendLine(string.Format("{0}\t\tprivate string[] _Strings;", indent));
				content.AppendLine(string.Format("{0}\t\tpublic string[] strings {{ get {{ return _Strings; }} }}", indent));
			}
			if (hasLang) {
				content.AppendLine(string.Format("{0}\t\tprivate Func<string, string> _Translate;", indent));
				content.AppendLine(string.Format("{0}\t\tpublic string Translate(string key) {{", indent));
				content.AppendLine(string.Format("{0}\t\t\treturn _Translate == null ? key : _Translate(key);", indent));
				content.AppendLine(string.Format("{0}\t\t}}", indent));
			}
			if (hasRich) {
				content.AppendLine(string.Format("{0}\t\tprivate Func<string, string> _Enrich;", indent));
				content.AppendLine(string.Format("{0}\t\tpublic string Enrich(string key) {{", indent));
				content.AppendLine(string.Format("{0}\t\t\treturn _Enrich == null ? key : _Enrich(key);", indent));
				content.AppendLine(string.Format("{0}\t\t}}", indent));
			}
			foreach (SheetData sheet in sheets) {
				FieldData firstField = sheet.fields[0];
				string idVarName = firstField.fieldName;
				if (sheet.keyToMultiValues) {
					content.AppendLine(string.Format("{0}\t\tprivate Func<{1}, List<{2}>, int> _Get{2}List;",
						indent, GetFieldTypeName(firstField.fieldType), sheet.itemClassName));
					content.AppendLine(string.Format("{0}\t\tpublic int Get{1}List({2} {3}, List<{1}> items) {{",
						indent, sheet.itemClassName, GetFieldTypeName(firstField.fieldType), idVarName));
					content.AppendLine(string.Format("{0}\t\t\treturn _Get{1}List({2}, items);",
						indent, sheet.itemClassName, idVarName));
					content.AppendLine(string.Format("{0}\t\t}}", indent));
				} else {
					content.AppendLine(string.Format("{0}\t\tprivate Func<{1}, {2}> _Get{2};",
						indent, GetFieldTypeName(firstField.fieldType), sheet.itemClassName));
					content.AppendLine(string.Format("{0}\t\tpublic {1} Get{1}({2} {3}) {{",
						indent, sheet.itemClassName, GetFieldTypeName(firstField.fieldType), idVarName));
					content.AppendLine(string.Format("{0}\t\t\treturn _Get{1}({2});",
						indent, sheet.itemClassName, idVarName));
					content.AppendLine(string.Format("{0}\t\t}}", indent));
				}
			}
			content.AppendFormat("{0}\t\tpublic DataGetter(", indent);
			bool first = true;
			if (settings.use_hash_string) { content.Append("string[] strings"); first = false; }
			if (hasLang) {
				if (first) { first = false; } else { content.Append(", "); }
				content.Append("Func<string, string> translate");
			}
			if (hasRich) {
				if (first) { first = false; } else { content.Append(", "); }
				content.Append("Func<string, string> enrich");
			}
			foreach (SheetData sheet in sheets) {
				FieldData firstField = sheet.fields[0];
				if (first) { first = false; } else { content.Append(", "); }
				if (sheet.keyToMultiValues) {
					content.AppendFormat("Func<{0}, List<{1}>, int> get{1}List",
						GetFieldTypeName(firstField.fieldType), sheet.itemClassName);
				} else {
					content.AppendFormat("Func<{0}, {1}> get{1}",
						GetFieldTypeName(firstField.fieldType), sheet.itemClassName);
				}
			}
			content.AppendLine(") {");
			if (settings.use_hash_string) {
				content.AppendLine(string.Format("{0}\t\t\t_Strings = strings;", indent));
			}
			if (hasLang) {
				content.AppendLine(string.Format("{0}\t\t\t_Translate = translate;", indent));
			}
			if (hasRich) {
				content.AppendLine(string.Format("{0}\t\t\t_Enrich = enrich;", indent));
			}
			foreach (SheetData sheet in sheets) {
				FieldData firstField = sheet.fields[0];
				if (sheet.keyToMultiValues) {
					content.AppendLine(string.Format("{0}\t\t\t_Get{1}List = get{1}List;",
						indent, sheet.itemClassName));
				} else {
					content.AppendLine(string.Format("{0}\t\t\t_Get{1} = get{1};",
						indent, sheet.itemClassName));
				}
			}
			content.AppendLine(string.Format("{0}\t\t}}", indent));
			content.AppendLine(string.Format("{0}\t}}", indent));
			content.AppendLine();
			content.AppendLine(string.Format("{0}\t[NonSerialized]", indent));
			content.AppendLine(string.Format("{0}\tprivate DataGetter mDataGetterObject;", indent));
			content.AppendLine(string.Format("{0}\tprivate DataGetter DataGetterObject {{", indent));
			content.AppendLine(string.Format("{0}\t\tget {{", indent));
			content.AppendLine(string.Format("{0}\t\t\tif (mDataGetterObject == null) {{", indent));
			content.AppendFormat("{0}\t\t\t\tmDataGetterObject = new DataGetter(", indent);
			first = true;
			if (settings.use_hash_string) { content.Append("_HashStrings"); first = false; }
			if (hasLang) {
				if (first) { first = false; } else { content.Append(", "); }
				content.Append("Translate");
			}
			if (hasRich) {
				if (first) { first = false; } else { content.Append(", "); }
				content.Append("Enrich");
			}
			foreach (SheetData sheet in sheets) {
				FieldData firstField = sheet.fields[0];
				if (first) { first = false; } else { content.Append(", "); }
				if (sheet.keyToMultiValues) {
					content.AppendFormat("Get{0}List", sheet.itemClassName);
				} else {
					content.AppendFormat("Get{0}", sheet.itemClassName);
				}
			}
			content.AppendLine(");");
			content.AppendLine(string.Format("{0}\t\t\t}}", indent));
			content.AppendLine(string.Format("{0}\t\t\treturn mDataGetterObject;", indent));
			content.AppendLine(string.Format("{0}\t\t}}", indent));
			content.AppendLine(string.Format("{0}\t}}", indent));
			content.AppendLine(string.Format("{0}}}", indent));
			content.AppendLine();

			foreach (SheetData sheet in sheets) {
				content.AppendLine(string.Format("{0}[Serializable]", indent));
				content.AppendLine(string.Format("{0}public class {1} {{", indent, sheet.itemClassName));
				content.AppendLine();
				customTypeIndices.Clear();
				foreach (FieldData field in sheet.fields) {
					string fieldTypeNameScript = null;
					switch (field.fieldType) {
						case eFieldTypes.Unknown:
							fieldTypeNameScript = string.Concat(className, ".", field.fieldTypeName);
							break;
						case eFieldTypes.CustomType:
						case eFieldTypes.ExternalEnum:
							fieldTypeNameScript = field.fieldTypeName;
							break;
						case eFieldTypes.UnknownList:
							fieldTypeNameScript = string.Concat(className, ".", field.fieldTypeName, "[]");
							break;
						case eFieldTypes.CustomTypeList:
							fieldTypeNameScript = field.fieldTypeName + "[]";
							break;
						default:
							fieldTypeNameScript = GetFieldTypeName(field.fieldType);
							break;
					}
					string capitalFieldName = CapitalFirstChar(field.fieldName);
					content.AppendLine(string.Format("{0}\t{1}", indent, serializeAttribute));
					if (settings.use_hash_string && (field.fieldType == eFieldTypes.String || field.fieldType == eFieldTypes.Strings)) {
						content.AppendLine(string.Format("{0}\tprivate {1} _{2};",
							indent, field.fieldType == eFieldTypes.Strings ? "int[]" : "int", capitalFieldName));
						content.AppendLine(string.Format("{0}\tprivate {1} _{2}_;", indent, fieldTypeNameScript, capitalFieldName));
						content.AppendLine(string.Format("{0}\tpublic {1} {2} {{ get {{ return _{3}_; }} }}", indent, fieldTypeNameScript, field.fieldName, capitalFieldName));
					} else if (settings.compress_color_into_int && field.fieldType == eFieldTypes.Color) {
						content.AppendLine(string.Format("{0}\tprivate int _{1};", indent, capitalFieldName));
						content.AppendLine(string.Format("{0}\tpublic {1} {2} {{", indent, fieldTypeNameScript, field.fieldName));
						content.AppendLine(string.Format("{0}\t\tget {{", indent));
						content.AppendLine(string.Format("{0}\t\t\tfloat inv = 1f / 255f;", indent));
						content.AppendLine(string.Format("{0}\t\t\tColor c = Color.black;", indent));
						content.AppendLine(string.Format("{0}\t\t\tc.r = inv * ((_{1} >> 24) & 0xFF);", indent, capitalFieldName));
						content.AppendLine(string.Format("{0}\t\t\tc.g = inv * ((_{1} >> 16) & 0xFF);", indent, capitalFieldName));
						content.AppendLine(string.Format("{0}\t\t\tc.b = inv * ((_{1} >> 8) & 0xFF);", indent, capitalFieldName));
						content.AppendLine(string.Format("{0}\t\t\tc.a = inv * (_{1} & 0xFF);", indent, capitalFieldName));
						content.AppendLine(string.Format("{0}\t\t\treturn c;", indent));
						content.AppendLine(string.Format("{0}\t\t}}", indent));
						content.AppendLine(string.Format("{0}\t}}", indent));
					} else if (field.fieldType == eFieldTypes.Lang || field.fieldType == eFieldTypes.Rich) {
						if (settings.use_hash_string) {
							content.AppendLine(string.Format("{0}\tprivate int _{1};", indent, capitalFieldName));
						} else {
							content.AppendLine(string.Format("{0}\tprivate {1} _{2};", indent, fieldTypeNameScript, capitalFieldName));
						}
						content.AppendLine(string.Format("{0}\tprivate {1} _{2}_;", indent, fieldTypeNameScript, capitalFieldName));
						content.AppendLine(string.Format("{0}\tpublic {1} {2} {{ get {{ return _{3}_; }} }}", indent, fieldTypeNameScript, field.fieldName, capitalFieldName));
					} else if (field.fieldType == eFieldTypes.Langs || field.fieldType == eFieldTypes.Riches) {
						if (settings.use_hash_string) {
							content.AppendLine(string.Format("{0}\tprivate int[] _{1};", indent, capitalFieldName));
						} else {
							content.AppendLine(string.Format("{0}\tprivate {1} _{2};", indent, fieldTypeNameScript, capitalFieldName));
						}
						content.AppendLine(string.Format("{0}\tprivate {1} _{2}_;", indent, fieldTypeNameScript, capitalFieldName));
						content.AppendLine(string.Format("{0}\tpublic {1} {2} {{ get {{ return _{3}_; }} }}", indent, fieldTypeNameScript, field.fieldName, capitalFieldName));
					} else if (field.fieldType == eFieldTypes.CustomType) {
						string keyFlag = customTypes[field.fieldTypeName][0];
						switch (keyFlag[0]) {
							case 'l':
								content.AppendLine(string.Format("{0}\tprivate long _{1};", indent, capitalFieldName));
								break;
							case 's':
								content.AppendLine(string.Format("{0}\tprivate string _{1};", indent, capitalFieldName));
								break;
							default:
								content.AppendLine(string.Format("{0}\tprivate int _{1};", indent, capitalFieldName));
								break;
						}
						if (keyFlag.Length > 1) {
							content.AppendLine(string.Format("{0}\tprivate static List<{1}> s_{2} = new List<{1}>();", indent, fieldTypeNameScript, capitalFieldName));
							content.AppendLine(string.Format("{0}\tprivate {1}[] _{2}_;", indent, fieldTypeNameScript, capitalFieldName));
							content.AppendLine(string.Format("{0}\tpublic {1}[] {2} {{", indent, fieldTypeNameScript, field.fieldName));
							content.AppendLine(string.Format("{0}\t\tget {{", indent));
							content.AppendLine(string.Format("{0}\t\t\treturn _{1}_;", indent, capitalFieldName));
							content.AppendLine(string.Format("{0}\t\t}}", indent));
							content.AppendLine(string.Format("{0}\t}}", indent));
						} else {
							content.AppendLine(string.Format("{0}\tprivate {1} _{2}_;", indent, fieldTypeNameScript, capitalFieldName));
							content.AppendLine(string.Format("{0}\tpublic {1} {2} {{", indent, fieldTypeNameScript, field.fieldName));
							content.AppendLine(string.Format("{0}\t\tget {{", indent));
							content.AppendLine(string.Format("{0}\t\t\treturn _{1}_;", indent, capitalFieldName));
							content.AppendLine(string.Format("{0}\t\t}}", indent));
							content.AppendLine(string.Format("{0}\t}}", indent));
						}
					} else if (field.fieldType == eFieldTypes.CustomTypeList) {
						string keyFlag = customTypes[field.fieldTypeName][0];
						switch (keyFlag[0]) {
							case 'l':
								content.AppendLine(string.Format("{0}\tprivate long[] _{1};", indent, capitalFieldName));
								break;
							case 's':
								content.AppendLine(string.Format("{0}\tprivate string[] _{1};", indent, capitalFieldName));
								break;
							default:
								content.AppendLine(string.Format("{0}\tprivate int[] _{1};", indent, capitalFieldName));
								break;
						}
						if (keyFlag.Length > 1) {
							content.AppendLine(string.Format("{0}\tprivate static List<{1}> s_{2} = new List<{1}>();", indent, field.fieldTypeName, capitalFieldName));
						}
						content.AppendLine(string.Format("{0}\tprivate {1} _{2}_;", indent, fieldTypeNameScript, capitalFieldName));
						content.AppendLine(string.Format("{0}\tpublic {1} {2} {{ get {{ return _{3}_; }} }}",
							indent, fieldTypeNameScript, field.fieldName, capitalFieldName));
					} else {
						content.AppendLine(string.Format("{0}\tprivate {1} _{2};", indent, fieldTypeNameScript, capitalFieldName));
						content.AppendLine(string.Format("{0}\tpublic {1} {2} {{ get {{ return _{3}; }} }}",
							indent, fieldTypeNameScript, field.fieldName, capitalFieldName));
					}
					content.AppendLine();
				}
				bool hashStringKey = sheet.fields[0].fieldType == eFieldTypes.String && settings.use_hash_string;
				content.AppendLine(string.Format("{0}\t[NonSerialized]", indent));
				content.AppendLine(string.Format("{0}\tprivate int mVersion = 0;", indent));
				content.AppendLine(string.Format("{0}\tpublic {1} Init(int version, {2}.IDataGetter getter{3}) {{",
					indent, sheet.itemClassName, className, hashStringKey ? ", bool keyOnly" : ""));
				content.AppendLine(string.Format("{0}\t\tif (mVersion == version) {{ return this; }}", indent));
				bool firstField = true;
				foreach (FieldData field in sheet.fields) {
					string capitalFieldName = CapitalFirstChar(field.fieldName);
					switch (field.fieldType) {
						case eFieldTypes.String:
							if (settings.use_hash_string) {
								content.AppendLine(string.Format("{0}\t\t_{1}_ = getter.strings[_{1}];",
									indent, capitalFieldName));
							}
							break;
						case eFieldTypes.Strings:
							if (settings.use_hash_string) {
								content.AppendLine(string.Format("{0}\t\tint len{1} = _{1}.Length;",
									indent, capitalFieldName));
								content.AppendLine(string.Format("{0}\t\t_{1}_ = new string[len{1}];",
									indent, capitalFieldName));
								content.AppendLine(string.Format("{0}\t\tfor (int i = 0; i < len{1}; i++) {{",
									indent, capitalFieldName));
								content.AppendLine(string.Format("{0}\t\t\t_{1}_[i] = getter.strings[_{1}[i]];",
									indent, capitalFieldName, field.fieldTypeName));
								content.AppendLine(string.Format("{0}\t\t}}", indent));
							}
							break;
						case eFieldTypes.Lang:
							if (settings.use_hash_string) {
								content.AppendLine(string.Format("{0}\t\t_{1}_ = getter.Translate(getter.strings[_{1}]);",
									indent, capitalFieldName));
							} else {
								content.AppendLine(string.Format("{0}\t\t_{1}_ = getter.Translate(_{1});",
									indent, capitalFieldName));
							}
							break;
						case eFieldTypes.Langs:
							content.AppendLine(string.Format("{0}\t\tint len{1} = _{1}.Length;",
								indent, capitalFieldName));
							content.AppendLine(string.Format("{0}\t\t_{1}_ = new string[len{1}];",
								indent, capitalFieldName));
							content.AppendLine(string.Format("{0}\t\tfor (int i = 0; i < len{1}; i++) {{",
								indent, capitalFieldName));
							if (settings.use_hash_string) {
								content.AppendLine(string.Format("{0}\t\t\t_{1}_[i] = getter.Translate(getter.strings[_{1}[i]]);",
									indent, capitalFieldName, field.fieldTypeName));
							} else {
								content.AppendLine(string.Format("{0}\t\t\t_{1}_[i] = getter.Translate(_{1}[i]);",
									indent, capitalFieldName, field.fieldTypeName));
							}
							content.AppendLine(string.Format("{0}\t\t}}", indent));
							break;
						case eFieldTypes.Rich:
							if (settings.use_hash_string) {
								content.AppendLine(string.Format("{0}\t\t_{1}_ = getter.Enrich(getter.strings[_{1}]);",
									indent, capitalFieldName));
							} else {
								content.AppendLine(string.Format("{0}\t\t_{1}_ = getter.Enrich(_{1});",
									indent, capitalFieldName));
							}
							break;
						case eFieldTypes.Riches:
							content.AppendLine(string.Format("{0}\t\tint len{1} = _{1}.Length;",
								indent, capitalFieldName));
							content.AppendLine(string.Format("{0}\t\t_{1}_ = new string[len{1}];",
								indent, capitalFieldName));
							content.AppendLine(string.Format("{0}\t\tfor (int i = 0; i < len{1}; i++) {{",
								indent, capitalFieldName));
							if (settings.use_hash_string) {
								content.AppendLine(string.Format("{0}\t\t\t_{1}_[i] = getter.Enrich(getter.strings[_{1}[i]]);",
									indent, capitalFieldName, field.fieldTypeName));
							} else {
								content.AppendLine(string.Format("{0}\t\t\t_{1}_[i] = getter.Enrich(_{1}[i]);",
									indent, capitalFieldName, field.fieldTypeName));
							}
							content.AppendLine(string.Format("{0}\t\t}}", indent));
							break;
						case eFieldTypes.CustomType:
							if (customTypes[field.fieldTypeName][0].Length > 1) {
								content.AppendLine(string.Format("{0}\t\ts_{1}.Clear();", indent, capitalFieldName));
								content.AppendLine(string.Format("{0}\t\tgetter.Get{2}List(_{1}, s_{1});",
									indent, capitalFieldName, field.fieldTypeName));
								content.AppendLine(string.Format("{0}\t\t_{1}_ = s_{1}.ToArray();", indent, capitalFieldName));
							} else {
								content.AppendLine(string.Format("{0}\t\t_{1}_ = getter.Get{2}(_{1});",
									indent, capitalFieldName, field.fieldTypeName));
							}
							break;
						case eFieldTypes.CustomTypeList:
							content.AppendLine(string.Format("{0}\t\tint len{1} = _{1}.Length;",
								indent, capitalFieldName));
							if (customTypes[field.fieldTypeName][0].Length > 1) {
								content.AppendLine(string.Format("{0}\t\ts_{1}.Clear();", indent, capitalFieldName));
								content.AppendLine(string.Format("{0}\t\tfor (int i = 0; i < len{1}; i++) {{",
									indent, capitalFieldName));
								content.AppendLine(string.Format("{0}\t\t\tgetter.Get{2}List(_{1}[i], s_{1});",
									indent, capitalFieldName, field.fieldTypeName));
								content.AppendLine(string.Format("{0}\t\t}}", indent));
								content.AppendLine(string.Format("{0}\t\t_{1}_ = s_{1}.ToArray();", indent, capitalFieldName));
							} else {
								content.AppendLine(string.Format("{0}\t\t_{1}_ = new {2}[len{1}];",
									indent, capitalFieldName, field.fieldTypeName));
								content.AppendLine(string.Format("{0}\t\tfor (int i = 0; i < len{1}; i++) {{",
									indent, capitalFieldName));
								content.AppendLine(string.Format("{0}\t\t\t_{1}_[i] = getter.Get{2}(_{1}[i]);",
									indent, capitalFieldName, field.fieldTypeName));
								content.AppendLine(string.Format("{0}\t\t}}", indent));
							}
							break;
					}
					if (!firstField) { continue; }
					firstField = false;
					if (!hashStringKey) { continue; }
					content.AppendLine(string.Format("{0}\t\tif (keyOnly) {{ return this; }}", indent));
				}
				content.AppendLine(string.Format("{0}\t\tmVersion = version;", indent));
				content.AppendLine(string.Format("{0}\t\treturn this;", indent));
				content.AppendLine(string.Format("{0}\t}}", indent));
				content.AppendLine();
				if (settings.generate_tostring_method) {
					content.AppendLine(string.Format("{0}\tpublic override string ToString() {{", indent));
					List<string> toStringFormats = new List<string>();
					List<string> toStringValues = new List<string>();
					bool toStringContainsArray = false;
					for (int i = 0, imax = sheet.fields.Count; i < imax; i++) {
						FieldData field = sheet.fields[i];
						toStringFormats.Add(string.Format("{0}:{{{1}}}", field.fieldName, i));
						bool isArray = field.fieldType == eFieldTypes.Floats || field.fieldType == eFieldTypes.Ints ||
							field.fieldType == eFieldTypes.Longs || field.fieldType == eFieldTypes.Strings ||
							field.fieldType == eFieldTypes.UnknownList || field.fieldType == eFieldTypes.CustomTypeList;
						if (field.fieldType == eFieldTypes.CustomType) {
							if (customTypes[field.fieldTypeName][0].Length > 1) { isArray = true; }
						}
						if (isArray) {
							toStringValues.Add(string.Format("array2string({0})", field.fieldName));
						} else {
							toStringValues.Add(field.fieldName);
						}
						if (!toStringContainsArray) {
							toStringContainsArray = isArray;
						}
					}
					content.AppendLine(string.Format("{0}\t\treturn string.Format(\"[{1}]{{{{{2}}}}}\",",
						indent, sheet.itemClassName, string.Join(", ", toStringFormats.ToArray())));
					content.AppendLine(string.Format("{0}\t\t\t{1});", indent, string.Join(", ", toStringValues.ToArray())));
					content.AppendLine(string.Format("{0}\t}}", indent));
					content.AppendLine();
					if (toStringContainsArray) {
						content.AppendLine(string.Format("{0}\tprivate string array2string(Array array) {{", indent));
						content.AppendLine(string.Format("{0}\t\tint len = array.Length;", indent));
						content.AppendLine(string.Format("{0}\t\tstring[] strs = new string[len];", indent));
						content.AppendLine(string.Format("{0}\t\tfor (int i = 0; i < len; i++) {{", indent));
						content.AppendLine(string.Format("{0}\t\t\tstrs[i] = string.Format(\"{{0}}\", array.GetValue(i));", indent));
						content.AppendLine(string.Format("{0}\t\t}}", indent));
						content.AppendLine(string.Format("{0}\t\treturn string.Concat(\"[\", string.Join(\", \", strs), \"]\");", indent));
						content.AppendLine(string.Format("{0}\t}}", indent));
						content.AppendLine();
					}
				}
				content.AppendLine(string.Format("{0}}}", indent));
				content.AppendLine();
			}
			if (!string.IsNullOrEmpty(settings.name_space)) {
				content.AppendLine("}");
			}

			if (!Directory.Exists(settings.script_directory)) {
				Directory.CreateDirectory(settings.script_directory);
			}
			string scriptPath = null;
			if (settings.script_directory.EndsWith("/")) {
				scriptPath = string.Concat(settings.script_directory, className, ".cs");
			} else {
				scriptPath = string.Concat(settings.script_directory, "/", className, ".cs");
			}
			string fileMD5 = null;
			MD5CryptoServiceProvider md5Calc = null;
			if (File.Exists(scriptPath)) {
				md5Calc = new MD5CryptoServiceProvider();
				try {
					using (FileStream fs = File.OpenRead(scriptPath)) {
						fileMD5 = BitConverter.ToString(md5Calc.ComputeHash(fs));
					}
				} catch (Exception e) { Debug.LogException(e); }
			}
			byte[] bytes = Encoding.UTF8.GetBytes(content.ToString());
			bool toWrite = true;
			if (!string.IsNullOrEmpty(fileMD5)) {
				if (BitConverter.ToString(md5Calc.ComputeHash(bytes)) == fileMD5) {
					toWrite = false;
				}
			}
			EditorUtility.ClearProgressBar();
			if (toWrite) { File.WriteAllBytes(scriptPath, bytes); }
			return true;
		}

		static bool FlushData(FlushDataSettings settings) {
			List<SheetData> sheets = new List<SheetData>();
			Dictionary<string, List<string>> unknownTypes = new Dictionary<string, List<string>>();
			Dictionary<string, List<string>> customTypes = new Dictionary<string, List<string>>();
			string className;
			bool hasLang;
			bool hasRich;
			if (!ReadExcel(settings.excel_path, settings.treat_unknown_types_as_enum, sheets, unknownTypes, customTypes, out className, out hasLang, out hasRich)) { return false; }

			if (!Directory.Exists(settings.asset_directory)) {
				Directory.CreateDirectory(settings.asset_directory);
			}
			AssetDatabase.Refresh();

			Dictionary<string, List<string>> enumTypes = new Dictionary<string, List<string>>();
			foreach (KeyValuePair<string, List<string>> kv in unknownTypes) {
				Type type = GetExternalType(settings.class_name + "+" + kv.Key);
				if (type == null || !type.IsEnum) {
					Debug.LogErrorFormat("Cannot find enum type : {0}", settings.class_name + "+" + kv.Key);
					continue;
				}
				Array values = Enum.GetValues(type);
				int n = values.Length;
				List<string> enums = new List<string>(n);
				for (int i = 0; i < n; i++) {
					enums.Add(values.GetValue(i).ToString());
				}
				enumTypes.Add(kv.Key, enums);
			}

			string assetPath = null;
			if (settings.asset_directory.EndsWith("/")) {
				assetPath = string.Concat(settings.asset_directory, className, ".asset");
			} else {
				assetPath = string.Concat(settings.asset_directory, "/", className, ".asset");
			}
			ScriptableObject obj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(ScriptableObject)) as ScriptableObject;
			bool isAlreadyExists = true;
			if (obj == null) {
				obj = ScriptableObject.CreateInstance(settings.class_name);
				AssetDatabase.CreateAsset(obj, assetPath);
				isAlreadyExists = false;
			}

			Dictionary<string, int> hashStrings = new Dictionary<string, int>();
			SerializedObject so = new SerializedObject(obj);

			if (isAlreadyExists) {
				SerializedProperty pStrings = so.FindProperty("_HashStrings");
				if (pStrings != null) { pStrings.ClearArray(); }
			}
			foreach (SheetData sheet in sheets) {
				if (!sheet.internalData) { continue; }
				List<string> keys;
				if (!customTypes.TryGetValue(sheet.itemClassName, out keys)) { continue; }
				for (int i = sheet.indices.Count - 1; i >= 0; i--) {
					object[] items = sheet.table.Rows[sheet.indices[i]].ItemArray;
					int firstIndex = sheet.fields[0].fieldIndex;
					object item = firstIndex < items.Length ? items[firstIndex] : null;
					string key = item == null ? "" : item.ToString().Trim();
					if (keys.Contains(key)) { continue; }
					sheet.indices.RemoveAt(i);
				}
			}
			try {
				List<string> invalidFields = new List<string>();
				foreach (SheetData sheet in sheets) {
					invalidFields.Clear();
					SerializedProperty pItems = so.FindProperty(string.Format("_{0}Items", sheet.itemClassName));
					pItems.ClearArray();
					for (int i = 0, imax = sheet.indices.Count; i < imax; i++) {
						if (EditorUtility.DisplayCancelableProgressBar("Excel", string.Format("Serializing datas... {0} / {1}", i, imax), (i + 0f) / imax)) {
							EditorUtility.ClearProgressBar();
							return false;
						}
						pItems.InsertArrayElementAtIndex(0);
						SerializedProperty pItem = pItems.GetArrayElementAtIndex(0);
						object[] items = sheet.table.Rows[sheet.indices[i]].ItemArray;
						int numItems = items.Length;
						int firstIndex = sheet.fields[0].fieldIndex;
						foreach (FieldData field in sheet.fields) {
							SerializedProperty pField = pItem.FindPropertyRelative("_" + CapitalFirstChar(field.fieldName));
							if (pField == null) {
								if (!invalidFields.Contains(field.fieldName)) {
									invalidFields.Add(field.fieldName);
									Debug.LogErrorFormat("Field '{0}' not found in {1} sheet '{2}' !",
										field.fieldName, settings.excel_path, sheet.itemClassName);
								}
								continue;
							}
							int itemIndex = field.fieldIndex;
							object item = itemIndex < numItems ? items[itemIndex] : null;
							string value = item == null ? "" : item.ToString().Trim();
							if (itemIndex == firstIndex && string.IsNullOrEmpty(value)) { continue; }
							switch (field.fieldType) {
								case eFieldTypes.Bool:
									bool boolValue;
									if (bool.TryParse(value, out boolValue)) {
										pField.boolValue = boolValue;
									} else {
										pField.boolValue = value == "1" || value.ToLower() == "yes";
									}
									break;
								case eFieldTypes.Int:
									int intValue;
									if (int.TryParse(value, out intValue)) {
										pField.intValue = intValue;
									} else {
										pField.intValue = 0;
									}
									break;
								case eFieldTypes.Ints:
									int[] ints = GetIntsFromString(value);
									pField.ClearArray();
									for (int k = ints.Length - 1; k >= 0; k--) {
										pField.InsertArrayElementAtIndex(0);
										pField.GetArrayElementAtIndex(0).intValue = ints[k];
									}
									break;
								case eFieldTypes.Float:
									float floatValue;
									if (float.TryParse(value, out floatValue)) {
										pField.floatValue = floatValue;
									} else {
										pField.floatValue = 0f;
									}
									break;
								case eFieldTypes.Floats:
									float[] floats = GetFloatsFromString(value);
									pField.ClearArray();
									for (int k = floats.Length - 1; k >= 0; k--) {
										pField.InsertArrayElementAtIndex(0);
										pField.GetArrayElementAtIndex(0).floatValue = floats[k];
									}
									break;
								case eFieldTypes.Long:
									long longValue;
									if (long.TryParse(value, out longValue)) {
										pField.longValue = longValue;
									} else {
										pField.longValue = 0L;
									}
									break;
								case eFieldTypes.Vector2:
									float[] floatsV2 = GetFloatsFromString(value);
									pField.vector2Value = floatsV2.Length == 2 ? new Vector2(floatsV2[0], floatsV2[1]) : Vector2.zero;
									break;
								case eFieldTypes.Vector3:
									float[] floatsV3 = GetFloatsFromString(value);
									pField.vector3Value = floatsV3.Length == 3 ? new Vector3(floatsV3[0], floatsV3[1], floatsV3[2]) : Vector3.zero;
									break;
								case eFieldTypes.Vector4:
									float[] floatsV4 = GetFloatsFromString(value);
									pField.vector4Value = floatsV4.Length == 4 ? new Vector4(floatsV4[0], floatsV4[1], floatsV4[2], floatsV4[3]) : Vector4.zero;
									break;
								case eFieldTypes.Rect:
									float[] floatsRect = GetFloatsFromString(value);
									pField.rectValue = floatsRect.Length == 4 ? new Rect(floatsRect[0], floatsRect[1], floatsRect[2], floatsRect[3]) : new Rect();
									break;
								case eFieldTypes.Color:
									Color c = GetColorFromString(value);
									if (settings.compress_color_into_int) {
										int colorInt = 0;
										colorInt |= Mathf.RoundToInt(c.r * 255f) << 24;
										colorInt |= Mathf.RoundToInt(c.g * 255f) << 16;
										colorInt |= Mathf.RoundToInt(c.b * 255f) << 8;
										colorInt |= Mathf.RoundToInt(c.a * 255f);
										pField.intValue = colorInt;
									} else {
										pField.colorValue = c;
									}
									break;
								case eFieldTypes.String:
								case eFieldTypes.Lang:
								case eFieldTypes.Rich:
									if (settings.use_hash_string) {
										int stringIndex;
										if (!hashStrings.TryGetValue(value, out stringIndex)) {
											stringIndex = hashStrings.Count;
											hashStrings.Add(value, stringIndex);
										}
										pField.intValue = stringIndex;
									} else {
										pField.stringValue = value;
									}
									break;
								case eFieldTypes.Strings:
								case eFieldTypes.Langs:
								case eFieldTypes.Riches:
									string[] strs = GetStringsFromString(value);
									pField.ClearArray();
									if (settings.use_hash_string) {
										for (int k = strs.Length - 1; k >= 0; k--) {
											string str = strs[k];
											int stringIndex;
											if (!hashStrings.TryGetValue(str, out stringIndex)) {
												stringIndex = hashStrings.Count;
												hashStrings.Add(str, stringIndex);
											}
											pField.InsertArrayElementAtIndex(0);
											pField.GetArrayElementAtIndex(0).intValue = stringIndex;
										}
									} else {
										for (int k = strs.Length - 1; k >= 0; k--) {
											pField.InsertArrayElementAtIndex(0);
											pField.GetArrayElementAtIndex(0).stringValue = strs[k];
										}
									}
									break;
								case eFieldTypes.Unknown:
									List<string> enumValues1;
									if (enumTypes.TryGetValue(field.fieldTypeName, out enumValues1)) {
										pField.enumValueIndex = string.IsNullOrEmpty(value) ? 0 : enumValues1.IndexOf(value);
									}
									break;
								case eFieldTypes.UnknownList:
									pField.ClearArray();
									List<string> enumValues2;
									if (enumTypes.TryGetValue(field.fieldTypeName, out enumValues2)) {
										string[] evs = GetStringsFromString(value);
										for (int k = evs.Length - 1; k >= 0; k--) {
											string ev = evs[k];
											pField.InsertArrayElementAtIndex(0);
											pField.GetArrayElementAtIndex(0).enumValueIndex = string.IsNullOrEmpty(ev) ? 0 : enumValues2.IndexOf(ev);
										}
									}
									break;
								case eFieldTypes.CustomType:
									switch (customTypes[field.fieldTypeName][0][0]) {
										case 'l':
											long keyLong;
											if (long.TryParse(value, out keyLong)) {
												pField.longValue = keyLong;
											} else {
												pField.longValue = 0L;
											}
											break;
										case 's':
											pField.stringValue = value;
											break;
										default:
											int keyInt;
											if (int.TryParse(value, out keyInt)) {
												pField.intValue = keyInt;
											} else {
												pField.intValue = 0;
											}
											break;
									}
									break;
								case eFieldTypes.CustomTypeList:
									pField.ClearArray();
									switch (customTypes[field.fieldTypeName][0][0]) {
										case 'l':
											long[] ksl = GetLongsFromString(value);
											for (int k = ksl.Length - 1; k >= 0; k--) {
												pField.InsertArrayElementAtIndex(0);
												pField.GetArrayElementAtIndex(0).longValue = ksl[k];
											}
											break;
										case 's':
											string[] kss = GetStringsFromString(value);
											for (int k = kss.Length - 1; k >= 0; k--) {
												pField.InsertArrayElementAtIndex(0);
												pField.GetArrayElementAtIndex(0).stringValue = kss[k];
											}
											break;
										default:
											int[] ksi = GetIntsFromString(value);
											for (int k = ksi.Length - 1; k >= 0; k--) {
												pField.InsertArrayElementAtIndex(0);
												pField.GetArrayElementAtIndex(0).intValue = ksi[k];
											}
											break;
									}
									break;
								case eFieldTypes.ExternalEnum:
									pField.enumValueIndex = GetExternalTypeEnumValue(field.fieldTypeName, value);
									break;
							}
						}
					}
					if (settings.use_hash_string && hashStrings.Count > 0) {
						string[] strings = new string[hashStrings.Count];
						foreach (KeyValuePair<string, int> kv in hashStrings) {
							strings[kv.Value] = kv.Key;
						}
						SerializedProperty pStrings = so.FindProperty("_HashStrings");
						pStrings.ClearArray();
						int total = strings.Length;
						for (int i = strings.Length - 1; i >= 0; i--) {
							if (EditorUtility.DisplayCancelableProgressBar("Excel", string.Format("Writing hash strings ... {0} / {1}",
								total - i, total), (float)(total - i) / total)) {
								EditorUtility.ClearProgressBar();
								return false;
							}
							pStrings.InsertArrayElementAtIndex(0);
							SerializedProperty pString = pStrings.GetArrayElementAtIndex(0);
							pString.stringValue = strings[i];
						}
					}
				}
			} catch (Exception e) {
				Debug.LogException(e);
			}
			EditorUtility.ClearProgressBar();
			so.ApplyModifiedProperties();
			return true;
		}

		static bool ReadExcel(string excel_path, bool treat_unknown_types_as_enum, List<SheetData> sheets,
			Dictionary<string, List<string>> unknownTypes, Dictionary<string, List<string>> customTypes,
			out string className, out bool hasLang, out bool hasRich) {
			className = Path.GetFileNameWithoutExtension(excel_path);
			hasLang = false;
			hasRich = false;
			if (!CheckClassName(className)) {
				string msg = string.Format("Invalid excel file '{0}', because the name of the xlsx file should be a class name...", excel_path);
				EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
				return false;
			}
			int indexOfDot = excel_path.LastIndexOf('.');
			string tempExcel = string.Concat(excel_path.Substring(0, indexOfDot), "_temp_", excel_path.Substring(indexOfDot, excel_path.Length - indexOfDot));
			File.Copy(excel_path, tempExcel);
			Stream stream = null;
			try {
				stream = File.OpenRead(tempExcel);
			} catch {
				File.Delete(tempExcel);
				string msg = string.Format("Fail to open '{0}' because of sharing violation. Perhaps you should close your Excel application first...", excel_path);
				EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
				return false;
			}
			IExcelDataReader reader = excel_path.ToLower().EndsWith(".xls") ? ExcelReaderFactory.CreateBinaryReader(stream) : ExcelReaderFactory.CreateOpenXmlReader(stream);
			DataSet data = reader.AsDataSet();
			reader.Dispose();
			stream.Close();
			File.Delete(tempExcel);
			if (data == null) {
				string msg = string.Format("Fail to read '{0}'. It seems that it's not a proper xlsx file...", excel_path);
				EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
				return false;
			}
			foreach (DataTable table in data.Tables) {
				string tableName = table.TableName.Trim();
				if (tableName.StartsWith("#")) { continue; }
				SheetData sheet = new SheetData();
				sheet.table = table;
				if (table.Rows.Count < Mathf.Max(global_configs.field_row, global_configs.type_row) + 1) {
					EditorUtility.ClearProgressBar();
					string msg = string.Format("Fail to parse '{0}'. The excel file should contains at least 2 lines that specify the column names and their types...", excel_path);
					EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
					return false;
				}
				sheet.itemClassName = tableName;
				while (true) {
					bool flag = true;
					if (!sheet.keyToMultiValues) {
						if ((sheet.itemClassName.StartsWith("[") && sheet.itemClassName.EndsWith("]")) ||
							(sheet.itemClassName.StartsWith("{") && sheet.itemClassName.EndsWith("}"))) {
							sheet.keyToMultiValues = true;
							sheet.itemClassName = sheet.itemClassName.Substring(1, sheet.itemClassName.Length - 2).Trim();
							flag = false;
						} else if (sheet.itemClassName.EndsWith("[]") || sheet.itemClassName.EndsWith("{}")) {
							sheet.keyToMultiValues = true;
							sheet.itemClassName = sheet.itemClassName.Substring(0, sheet.itemClassName.Length - 2).Trim();
							flag = false;
						}
					}
					if (!sheet.internalData) {
						if (sheet.itemClassName.StartsWith(".")) {
							sheet.internalData = true;
							sheet.itemClassName = sheet.itemClassName.Substring(1, sheet.itemClassName.Length - 1).Trim();
							flag = false;
						}
					}
					if (flag) { break; }
				}
				if (!CheckClassName(sheet.itemClassName)) {
					EditorUtility.ClearProgressBar();
					string msg = string.Format("Invalid sheet name '{0}', because the name of the sheet should be a class name...", sheet.itemClassName);
					EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
					return false;
				}
				object[] items;
				items = table.Rows[global_configs.field_row].ItemArray;
				for (int i = 0, imax = items.Length; i < imax; i++) {
					string fieldName = items[i].ToString().Trim();
					if (string.IsNullOrEmpty(fieldName)) { break; }
					if (fieldName[0] == '#') { continue; }
					if (i > 0) {
						Func<int, string> picker = (int index) => {
							return index < 0 || index >= table.Rows.Count ? null : table.Rows[index].ItemArray[i].ToString().Trim();
						};
						fieldName = ExcelFieldFilterManager.FilterField(sheet.itemClassName, fieldName, picker);
						if (string.IsNullOrEmpty(fieldName)) { continue; }
					}
					if (!CheckFieldName(fieldName)) {
						EditorUtility.ClearProgressBar();
						string msg = string.Format("Fail to parse '{0}' because of invalid field name '{1}'...", excel_path, fieldName);
						EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
						return false;
					}
					FieldData field = new FieldData();
					field.fieldName = fieldName;
					field.fieldIndex = i;
					sheet.fields.Add(field);
				}
				if (sheet.fields.Count <= 0) {
					EditorUtility.ClearProgressBar();
					string msg = string.Format("Fail to parse '{0}' because of no appropriate field names...", excel_path);
					EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
					return false;
				}
				int firstIndex = sheet.fields[0].fieldIndex;
				items = table.Rows[global_configs.type_row].ItemArray;
				for (int i = 0, imax = sheet.fields.Count; i < imax; i++) {
					FieldData field = sheet.fields[i];
					string typeName = items[field.fieldIndex].ToString();
					eFieldTypes fieldType = GetFieldType(typeName);
					if (fieldType == eFieldTypes.Unknown) {
						fieldType = eFieldTypes.UnknownList;
						if (typeName.StartsWith("[") && typeName.EndsWith("]")) {
							typeName = typeName.Substring(1, typeName.Length - 2).Trim();
						} else if (typeName.EndsWith("[]")) {
							typeName = typeName.Substring(0, typeName.Length - 2).Trim();
						} else {
							fieldType = eFieldTypes.Unknown;
						}
						List<string> unknowsTypeValues = null;
						if (!unknownTypes.TryGetValue(typeName, out unknowsTypeValues)) {
							unknowsTypeValues = new List<string>();
							unknowsTypeValues.Add("Default");
							unknownTypes.Add(typeName, unknowsTypeValues);
						}
						for (int j = global_configs.data_from_row, jmax = table.Rows.Count; j < jmax; j++) {
							object[] enumObjs = table.Rows[j].ItemArray;
							string enumValue = field.fieldIndex < enumObjs.Length ? enumObjs[field.fieldIndex].ToString() : null;
							if (fieldType == eFieldTypes.UnknownList) {
								foreach (string ev in GetStringsFromString(enumValue)) {
									string tev = ev.Trim();
									if (string.IsNullOrEmpty(tev)) { continue; }
									if (!unknowsTypeValues.Contains(ev)) { unknowsTypeValues.Add(ev); }
								}
							} else {
								string tev = enumValue.Trim();
								if (string.IsNullOrEmpty(tev)) { continue; }
								if (!unknowsTypeValues.Contains(tev)) { unknowsTypeValues.Add(tev); }
							}
						}
					}
					field.fieldType = fieldType;
					field.fieldTypeName = typeName;
					if (fieldType == eFieldTypes.Lang || fieldType == eFieldTypes.Langs) {
						hasLang = true;
					}
					if (fieldType == eFieldTypes.Rich || fieldType == eFieldTypes.Riches) {
						hasRich = true;
					}
				}

				string err = null;
				if (sheet.fields[0].fieldType == eFieldTypes.Int) {
					SortedList<int, List<int>> ids = new SortedList<int, List<int>>();
					for (int i = global_configs.data_from_row, imax = table.Rows.Count; i < imax; i++) {
						if (EditorUtility.DisplayCancelableProgressBar("Excel", string.Format("Checking IDs or Keys... {0} / {1}", i - 1, imax - 2), (float)(i - 1) / (imax - 2))) {
							EditorUtility.ClearProgressBar();
							return false;
						}
						string idStr = table.Rows[i].ItemArray[firstIndex].ToString().Trim();
						if (string.IsNullOrEmpty(idStr) || idStr == "-" || idStr[0] == '#') { continue; }
						int id;
						if (!int.TryParse(idStr, out id)) {
							err = string.Format("Fail to parse '{0}' in line {1} because it seems not to be a 'int'", idStr, i);
							break;
						}
						List<int> idList;
						if (ids.TryGetValue(id, out idList)) {
							if (sheet.keyToMultiValues) {
								idList.Add(i);
							} else {
								err = string.Format("ID or key:{0} appeared more once...", id);
								break;
							}
						} else {
							idList = new List<int>();
							idList.Add(i);
							ids.Add(id, idList);
						}
					}
					foreach (KeyValuePair<int, List<int>> kv in ids) {
						sheet.indices.AddRange(kv.Value);
					}
				} else if (sheet.fields[0].fieldType == eFieldTypes.Long) {
					SortedList<long, List<int>> ids = new SortedList<long, List<int>>();
					for (int i = global_configs.data_from_row, imax = table.Rows.Count; i < imax; i++) {
						if (EditorUtility.DisplayCancelableProgressBar("Excel", string.Format("Checking IDs or Keys... {0} / {1}", i - 1, imax - 2), (float)(i - 1) / (imax - 2))) {
							EditorUtility.ClearProgressBar();
							return false;
						}
						string idStr = table.Rows[i].ItemArray[firstIndex].ToString().Trim();
						if (string.IsNullOrEmpty(idStr) || idStr == "-" || idStr[0] == '#') { continue; }
						long id;
						if (!long.TryParse(idStr, out id)) {
							err = string.Format("Fail to parse '{0}' in line {1} because it seems not to be a 'long'", idStr, i);
							break;
						}
						List<int> idList;
						if (ids.TryGetValue(id, out idList)) {
							if (sheet.keyToMultiValues) {
								idList.Add(i);
							} else {
								err = string.Format("ID or key:{0} appeared more once...", id);
								break;
							}
						} else {
							idList = new List<int>();
							idList.Add(i);
							ids.Add(id, idList);
						}
					}
					foreach (KeyValuePair<long, List<int>> kv in ids) {
						sheet.indices.AddRange(kv.Value);
					}
				} else if (sheet.fields[0].fieldType == eFieldTypes.String) {
					SortedList<string, List<int>> keys = new SortedList<string, List<int>>();
					for (int i = global_configs.data_from_row, imax = table.Rows.Count; i < imax; i++) {
						if (EditorUtility.DisplayCancelableProgressBar("Excel", string.Format("Checking IDs or Keys... {0} / {1}", i - 1, imax - 2), (i - 1f) / (imax - 2f))) {
							EditorUtility.ClearProgressBar();
							return false;
						}
						string key = table.Rows[i].ItemArray[firstIndex].ToString().Trim();
						if (string.IsNullOrEmpty(key) || key == "-" || key[0] == '#') { continue; }
						List<int> idList;
						if (keys.TryGetValue(key, out idList)) {
							if (sheet.keyToMultiValues) {
								idList.Add(i);
							} else {
								err = string.Format("ID or key:{0} appeared more once...", key);
								break;
							}
						} else {
							idList = new List<int>();
							idList.Add(i);
							keys.Add(key, idList);
						}
					}
					foreach (KeyValuePair<string, List<int>> kv in keys) {
						sheet.indices.AddRange(kv.Value);
					}
				}
				if (!string.IsNullOrEmpty(err)) {
					Debug.LogError(err);
					sheet.indices.Clear();
				}
				sheet.indices.Reverse();
				sheets.Add(sheet);
			}
			foreach (SheetData sheet in sheets) {
				List<string> values;
				if (!unknownTypes.TryGetValue(sheet.itemClassName, out values)) { continue; }
				unknownTypes.Remove(sheet.itemClassName);
				switch (sheet.fields[0].fieldType) {
					case eFieldTypes.Int:
						values[0] = sheet.keyToMultiValues ? "ii" : "i";
						break;
					case eFieldTypes.Long:
						values[0] = sheet.keyToMultiValues ? "ll" : "l";
						break;
					case eFieldTypes.String:
						values[0] = sheet.keyToMultiValues ? "ss" : "s";
						break;
				}
				customTypes.Add(sheet.itemClassName, values);
			}
			if (!treat_unknown_types_as_enum && unknownTypes.Count > 0) {
				string[] typeStrs = new string[unknownTypes.Count];
				int index = 0;
				foreach (KeyValuePair<string, List<string>> kv in unknownTypes) {
					typeStrs[index++] = kv.Key;
				}
				EditorUtility.ClearProgressBar();
				string msg = string.Format("Fail to parse '{0}' because of invalid field type{1} '{2}'...",
					excel_path, unknownTypes.Count > 1 ? "s" : "", string.Join(", ", typeStrs));
				EditorUtility.DisplayDialog("Excel To ScriptableObject", msg, "OK");
				return false;
			}
			foreach (SheetData sheet in sheets) {
				foreach (FieldData field in sheet.fields) {
					if (!customTypes.ContainsKey(field.fieldTypeName)) { continue; }
					field.fieldType = field.fieldType == eFieldTypes.UnknownList ? eFieldTypes.CustomTypeList : eFieldTypes.CustomType;
				}
			}
			EditorUtility.ClearProgressBar();
			return true;
		}

		private static int[] GetIntsFromString(string str) {
			str = TrimBracket(str);
			if (string.IsNullOrEmpty(str)) { return new int[0]; }
			string[] splits = str.Split(',');
			int[] ints = new int[splits.Length];
			for (int i = 0, imax = splits.Length; i < imax; i++) {
				int intValue;
				if (int.TryParse(splits[i].Trim(), out intValue)) {
					ints[i] = intValue;
				} else {
					ints[i] = 0;
				}
			}
			return ints;
		}

		private static long[] GetLongsFromString(string str) {
			str = TrimBracket(str);
			if (string.IsNullOrEmpty(str)) { return new long[0]; }
			string[] splits = str.Split(',');
			long[] longs = new long[splits.Length];
			for (int i = 0, imax = splits.Length; i < imax; i++) {
				long longValue;
				if (long.TryParse(splits[i].Trim(), out longValue)) {
					longs[i] = longValue;
				} else {
					longs[i] = 0L;
				}
			}
			return longs;
		}

		private static float[] GetFloatsFromString(string str) {
			str = TrimBracket(str);
			if (string.IsNullOrEmpty(str)) { return new float[0]; }
			string[] splits = str.Split(',');
			float[] floats = new float[splits.Length];
			for (int i = 0, imax = splits.Length; i < imax; i++) {
				float floatValue;
				if (float.TryParse(splits[i].Trim(), out floatValue)) {
					floats[i] = floatValue;
				} else {
					floats[i] = 0;
				}
			}
			return floats;
		}

		private static string[] GetStringsFromString(string str) {
			str = TrimBracket(str);
			if (string.IsNullOrEmpty(str)) { return new string[0]; }
			return str.Split(',');
		}

		private static Color GetColorFromString(string str) {
			if (string.IsNullOrEmpty(str)) { return Color.clear; }
			uint colorUInt;
			if (GetColorUIntFromString(str, out colorUInt)) {
				uint r = (colorUInt >> 24) & 0xffu;
				uint g = (colorUInt >> 16) & 0xffu;
				uint b = (colorUInt >> 8) & 0xffu;
				uint a = colorUInt & 0xffu;
				return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
			}
			str = TrimBracket(str);
			string[] splits = str.Split(',');
			if (splits.Length == 4) {
				int r, g, b, a;
				if (int.TryParse(splits[0].Trim(), out r) && int.TryParse(splits[1].Trim(), out g) &&
					int.TryParse(splits[2].Trim(), out b) && int.TryParse(splits[3].Trim(), out a)) {
					return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
				}
			} else if (splits.Length == 3) {
				int r, g, b;
				if (int.TryParse(splits[0].Trim(), out r) && int.TryParse(splits[1].Trim(), out g) &&
					int.TryParse(splits[2].Trim(), out b)) {
					return new Color(r / 255f, g / 255f, b / 255f);
				}
			}
			return Color.clear;
		}

		private static bool GetColorUIntFromString(string str, out uint color) {
			if (reg_color32.IsMatch(str)) {
				color = Convert.ToUInt32(str, 16);
			} else if (reg_color24.IsMatch(str)) {
				color = (Convert.ToUInt32(str, 16) << 8) | 0xffu;
			} else {
				color = 0u;
				return false;
			}
			return true;
		}

		enum eFieldTypes {
			Unknown, UnknownList, Bool, Int, Ints, Float, Floats, Long, Longs,
			Vector2, Vector3, Vector4, Rect, Color, String, Strings,
			Lang, Langs, Rich, Riches, CustomType, CustomTypeList, ExternalEnum
		}

		static Dictionary<string, Type> s_external_enum_types = new Dictionary<string, Type>();

		static Type GetExternalType(string typename) {
			Type type;
			if (s_external_enum_types.TryGetValue(typename, out type)) {
				return type;
			}
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				type = Type.GetType(typename + "," + assembly.GetName().Name);
				if (type != null && type.IsEnum) {
					s_external_enum_types.Add(typename, type);
					return type;
				}
			}
			return null;
		}

		static int GetExternalTypeEnumValue(string typename, string value) {
			Type type = GetExternalType(typename);
			if (type == null) { return 0; }
			try { return (int)Enum.Parse(type, value); } catch { }
			return 0;
		}

		static eFieldTypes GetFieldType(string typename) {
			if (string.IsNullOrEmpty(typename)) { return eFieldTypes.Unknown; }
			eFieldTypes type = eFieldTypes.Unknown;
			switch (typename.Trim().ToLower()) {
				case "bool":
					type = eFieldTypes.Bool;
					break;
				case "int":
				case "int32":
					type = eFieldTypes.Int;
					break;
				case "ints":
				case "int[]":
				case "[int]":
				case "int32s":
				case "int32[]":
				case "[int32]":
					type = eFieldTypes.Ints;
					break;
				case "float":
					type = eFieldTypes.Float;
					break;
				case "floats":
				case "float[]":
				case "[float]":
					type = eFieldTypes.Floats;
					break;
				case "long":
				case "int64":
					type = eFieldTypes.Long;
					break;
				case "longs":
				case "long[]":
				case "[long]":
				case "int64s":
				case "int64[]":
				case "[int64]":
					type = eFieldTypes.Longs;
					break;
				case "vector2":
					type = eFieldTypes.Vector2;
					break;
				case "vector3":
					type = eFieldTypes.Vector3;
					break;
				case "vector4":
					type = eFieldTypes.Vector4;
					break;
				case "rect":
				case "rectangle":
					type = eFieldTypes.Rect;
					break;
				case "color":
				case "colour":
					type = eFieldTypes.Color;
					break;
				case "string":
					type = eFieldTypes.String;
					break;
				case "strings":
				case "string[]":
				case "[string]":
					type = eFieldTypes.Strings;
					break;
				case "lang":
				case "language":
					type = eFieldTypes.Lang;
					break;
				case "langs":
				case "lang[]":
				case "[lang]":
				case "languages":
				case "language[]":
				case "[language]":
					type = eFieldTypes.Langs;
					break;
				case "rich":
					type = eFieldTypes.Rich;
					break;
				case "richs":
				case "riches":
				case "rich[]":
				case "[rich]":
					type = eFieldTypes.Riches;
					break;
				default:
					if (GetExternalType(typename) != null) {
						type = eFieldTypes.ExternalEnum;
					}
					break;
			}
			return type;
		}

		static string GetFieldTypeName(eFieldTypes type) {
			string name = null;
			switch (type) {
				case eFieldTypes.Bool:
					name = "bool";
					break;
				case eFieldTypes.Int:
					name = "int";
					break;
				case eFieldTypes.Ints:
					name = "int[]";
					break;
				case eFieldTypes.Float:
					name = "float";
					break;
				case eFieldTypes.Floats:
					name = "float[]";
					break;
				case eFieldTypes.Long:
					name = "long";
					break;
				case eFieldTypes.Longs:
					name = "long[]";
					break;
				case eFieldTypes.Vector2:
					name = "Vector2";
					break;
				case eFieldTypes.Vector3:
					name = "Vector3";
					break;
				case eFieldTypes.Vector4:
					name = "Vector4";
					break;
				case eFieldTypes.Rect:
					name = "Rect";
					break;
				case eFieldTypes.Color:
					name = "Color";
					break;
				case eFieldTypes.String:
				case eFieldTypes.Lang:
				case eFieldTypes.Rich:
					name = "string";
					break;
				case eFieldTypes.Strings:
				case eFieldTypes.Langs:
				case eFieldTypes.Riches:
					name = "string[]";
					break;
			}
			return name;
		}

		static bool CheckClassName(string str) {
			return Regex.IsMatch(str, @"^[A-Z][A-Za-z0-9_]*$");
		}

		static bool CheckFieldName(string name) {
			return Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");
		}

		static string CapitalFirstChar(string str) {
			return str[0].ToString().ToUpper() + str.Substring(1);
		}

		static string TrimBracket(string str) {
			if (str.StartsWith("[") && str.EndsWith("]")) {
				return str.Substring(1, str.Length - 2);
			}
			return str;
		}

		static bool CheckIsNameSpaceValid(string ns) {
			if (string.IsNullOrEmpty(ns)) { return true; }
			return Regex.IsMatch(ns, @"(\S+\s*\.\s*)*\S+");
		}

		static bool CheckIsDirectoryValid(string path) {
			if (path == "Assets") { return true; }
			path = path.Replace('\\', '/');
			if (!path.StartsWith("Assets/")) { return false; }
			if (path.Contains("//")) { return false; }
			char[] invalidChars = Path.GetInvalidPathChars();
			string[] splits = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0, imax = splits.Length; i < imax; i++) {
				string dir = splits[i].Trim();
				if (string.IsNullOrEmpty(dir)) { return false; }
				if (dir.IndexOfAny(invalidChars) >= 0) { return false; }
			}
			return true;
		}

		static bool CheckProcessable(ExcelToScriptableObjectSetting setting) {
			return !string.IsNullOrEmpty(setting.excel_name) &&
						CheckIsNameSpaceValid(setting.name_space) &&
						CheckIsDirectoryValid(setting.script_directory) &&
						CheckIsDirectoryValid(setting.asset_directory);
		}

		static ExcelToScriptableObjectGlobalConfigs global_configs = new ExcelToScriptableObjectGlobalConfigs();
		static List<ExcelToScriptableObjectSetting> excel_settings = null;

		static void ReadsSettings() {
			excel_settings = new List<ExcelToScriptableObjectSetting>();
			string json = File.Exists(SETTINGS_PATH) ? File.ReadAllText(SETTINGS_PATH, Encoding.UTF8) : null;
			if (!string.IsNullOrEmpty(json)) {
				ExcelToScriptableObjectSettings settings = JsonUtility.FromJson<ExcelToScriptableObjectSettings>(json);
				global_configs = settings.configs;
				if (settings.excels != null) {
					excel_settings.AddRange(settings.excels);
				}
			}
			if (global_configs == null) { global_configs = new ExcelToScriptableObjectGlobalConfigs(); }
		}

		static void WriteSettings() {
			if (excel_settings == null) { return; }
			ExcelToScriptableObjectSettings data = new ExcelToScriptableObjectSettings();
			data.configs = global_configs;
			data.excels = excel_settings.ToArray();
			File.WriteAllText(SETTINGS_PATH, JsonUtility.ToJson(data, true), Encoding.UTF8);
		}

		private class ToProcess {
			public readonly List<GenerateCodeSettings> to_generate_code = new List<GenerateCodeSettings>();
			public readonly List<FlushDataSettings> to_flush_data = new List<FlushDataSettings>();
		}

		const float right = 96f;
		const float right_space = 100f;
		private static GUIContent s_content_slave = new GUIContent("Slave Excels :");

		private class ExcelToScriptableObjectSettingWrap {
			public string filterName;
			private Action<ExcelToScriptableObjectSettingWrap> mOnRemove;
			private bool mDirManualEdit;
			private int mScriptDirIndex;
			private int mAssetDirIndex;
			private string[] mFolders;
			private List<ExcelToScriptableObjectSlaveWrap> mSlavesData = new List<ExcelToScriptableObjectSlaveWrap>();
			private ReorderableList mSlavesList;
			private ToProcess mToProcess;
			public ExcelToScriptableObjectSettingWrap(ExcelToScriptableObjectSetting setting, Action<ExcelToScriptableObjectSettingWrap> onRemove) {
				this.setting = setting;
				mOnRemove = onRemove;
				mSlavesList = new ReorderableList(mSlavesData, typeof(ExcelToScriptableObjectSlaveWrap));
				mSlavesList.drawHeaderCallback = (Rect rect) => {
					EditorGUI.LabelField(rect, s_content_slave);
				};
				mSlavesList.onAddCallback = (ReorderableList list) => {
					ExcelToScriptableObjectSlave slave = new ExcelToScriptableObjectSlave();
					ExcelToScriptableObjectSlaveWrap wrap = new ExcelToScriptableObjectSlaveWrap(mSetting, slave);
					wrap.UpdateDirIndices(mFolders);
					mSlavesData.Add(wrap);
					int n = mSlavesData.Count;
					mSetting.slaves = new ExcelToScriptableObjectSlave[n];
					for (int i = 0; i < n; i++) {
						mSetting.slaves[i] = mSlavesData[i].slave;
					}
				};
				mSlavesList.elementHeightCallback = (int index) => {
					return mSlavesData[index].GetGUIHeight();
				};
				mSlavesList.drawElementBackgroundCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
					if (mSlavesData.Count <= 0) { return; }
					Color color = new Color(0f, 0f, 0f, 0.55f - 0.15f * (index & 1));
					if (isActive && isFocused) {
						color = new Color32(61, 96, 145, 255);
					}
					rect.x += 1f;
					rect.y += 1f;
					rect.height -= 1f;
					rect.width -= 3f;
					EditorGUI.DrawRect(rect, color);
				};
				mSlavesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
					mSlavesData[index].DrawGUI(rect, isActive, isFocused, mToProcess);
				};
			}
			private ExcelToScriptableObjectSetting mSetting;
			public ExcelToScriptableObjectSetting setting {
				get {
					return mSetting;
				}
				set {
					mSetting = value;
					if (mSetting.slaves == null) { mSetting.slaves = new ExcelToScriptableObjectSlave[0]; }
					int n = mSetting.slaves.Length;
					int m = mSlavesData.Count;
					for (int i = 0; i < n; i++) {
						ExcelToScriptableObjectSlave slave = mSetting.slaves[i];
						string key = slave.excel_name;
						bool flag = false;
						for (int j = i; j < m; j++) {
							ExcelToScriptableObjectSlaveWrap wrap = mSlavesData[j];
							if (wrap.slave.excel_name == key) {
								wrap.slave = slave;
								flag = true;
								if (i != j) {
									mSlavesData.RemoveAt(j);
									mSlavesData.Insert(i, wrap);
								}
								break;
							}
						}
						if (!flag) {
							mSlavesData.Insert(i, new ExcelToScriptableObjectSlaveWrap(mSetting, slave));
						}
					}
					for (int i = m - 1; i >= n; i--) { mSlavesData.RemoveAt(i); }
				}
			}
			public List<ExcelToScriptableObjectSlaveWrap> slaves { get { return mSlavesData; } }
			public void UpdateDirIndices(string[] folders) {
				mFolders = folders;
				mScriptDirIndex = -1;
				mAssetDirIndex = -1;
				for (int i = 0, imax = folders.Length; i < imax; i++) {
					string folder = folders[i];
					if (mSetting.script_directory == folder) { mScriptDirIndex = i; }
					if (mSetting.asset_directory == folder) { mAssetDirIndex = i; }
				}
				for (int i = 0, imax = mSlavesData.Count; i < imax; i++) {
					mSlavesData[i].UpdateDirIndices(folders);
				}
			}
			public float GetGUIHeight() {
				return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 7 + 4f + mSlavesList.GetHeight();
			}
			public void DrawGUI(Rect rect, bool isActive, bool isFocused, ToProcess toProcess) {
				Event evt = Event.current;
				mToProcess = toProcess;
				Rect pos = rect;
				pos.y += 2f;
				pos.height = EditorGUIUtility.singleLineHeight;
				pos.width = rect.width - right_space - 80f;
				bool excelInvalid = string.IsNullOrEmpty(mSetting.excel_name);
				EditorGUI.LabelField(pos, "Excel File", excelInvalid ? "(Not Selected)" : (string.IsNullOrEmpty(filterName) ? mSetting.excel_name : filterName), style_rich_text);
				Color cachedGUIBGColor = GUI.backgroundColor;
				if (excelInvalid) { GUI.backgroundColor = Color.green; }
				pos.x += pos.width;
				pos.width = 40f;
				if (GUI.Button(pos, "Select", EditorStyles.miniButton)) {
					string p = EditorUtility.OpenFilePanel("Select Excel File", ".", "xlsx");
					if (!string.IsNullOrEmpty(p)) {
						string projPath = Application.dataPath;
						projPath = projPath.Substring(0, projPath.Length - 6);
						if (p.StartsWith(projPath)) { p = p.Substring(projPath.Length, p.Length - projPath.Length); }
						mSetting.excel_name = p;
						GUI.changed = true;
					}
				}
				GUI.backgroundColor = cachedGUIBGColor;
				if (!excelInvalid) { GUI.backgroundColor = Color.green; }
				EditorGUI.BeginDisabledGroup(excelInvalid);
				pos.x += pos.width;
				if (GUI.Button(pos, "Open", EditorStyles.miniButton)) {
					if (evt.shift) {
						string folder = Path.GetDirectoryName(mSetting.excel_name);
						EditorUtility.OpenWithDefaultApp(folder);
					} else {
						EditorUtility.OpenWithDefaultApp(mSetting.excel_name);
					}
				}
				EditorGUI.EndDisabledGroup();
				GUI.backgroundColor = cachedGUIBGColor;
				pos.x = rect.x + rect.width - right;
				pos.width = right;
				bool processable = CheckProcessable(mSetting);
				cachedGUIBGColor = GUI.backgroundColor;
				GUI.backgroundColor = processable ? Color.green : Color.white;
				EditorGUI.BeginDisabledGroup(!processable);
				if (GUI.Button(pos, "Process Excel")) {
					toProcess.to_generate_code.Add(GetGenerateCodeSetting(mSetting));
					FlushDataSettings setting = GetFlushDataSettings(mSetting);
					toProcess.to_flush_data.Add(setting);
					if (!evt.shift) {
						for (int i = 0, imax = mSetting.slaves.Length; i < imax; i++) {
							ExcelToScriptableObjectSlave slave = mSetting.slaves[i];
							setting.excel_path = slave.excel_name;
							setting.asset_directory = slave.asset_directory;
							toProcess.to_flush_data.Add(setting);
						}
					}
				}
				GUI.backgroundColor = cachedGUIBGColor;
				EditorGUI.EndDisabledGroup();
				pos.x = rect.x;
				pos.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				pos.width = rect.width - right_space;
				pos.height = EditorGUIUtility.singleLineHeight;
				Rect posScript = pos;
				Rect posAsset = pos;
				posAsset.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				if (mDirManualEdit) {
					cachedGUIBGColor = GUI.backgroundColor;
					GUI.backgroundColor = CheckIsDirectoryValid(mSetting.script_directory) ? Color.white : Color.red;
					string scriptDirectory = EditorGUI.TextField(posScript, "Script Directory", mSetting.script_directory);
					if (mSetting.script_directory != scriptDirectory) {
						mSetting.script_directory = scriptDirectory.Replace('\\', '/');
					}
					GUI.backgroundColor = CheckIsDirectoryValid(mSetting.asset_directory) ? Color.white : Color.red;
					string assetDirectory = EditorGUI.TextField(posAsset, "Asset Directory", mSetting.asset_directory);
					if (mSetting.asset_directory != assetDirectory) {
						mSetting.asset_directory = assetDirectory.Replace('\\', '/');
					}
					GUI.backgroundColor = cachedGUIBGColor;
				} else {
					int scriptIndex = EditorGUI.Popup(posScript, "Script Directory", mScriptDirIndex, mFolders);
					int assetIndex = EditorGUI.Popup(posAsset, "Asset Directory", mAssetDirIndex, mFolders);
					if (scriptIndex != mScriptDirIndex) {
						mScriptDirIndex = scriptIndex;
						mSetting.script_directory = mFolders[scriptIndex];
					}
					if (assetIndex != mAssetDirIndex) {
						mAssetDirIndex = assetIndex;
						mSetting.asset_directory = mFolders[assetIndex];
					}
				}
				if (evt.type == EventType.MouseDown && evt.button == 0) {
					posScript.width = 120f;
					posAsset.width = 120f;
					if (evt.clickCount == 2) {
						if (posScript.Contains(evt.mousePosition)) {
							PingObject(setting.script_directory, setting.excel_name, ".cs");
							evt.Use();
						} else if (posAsset.Contains(evt.mousePosition)) {
							PingObject(setting.asset_directory, setting.excel_name, ".asset");
							evt.Use();
						}
					}
				}
				float cachedY = pos.y + (pos.height + EditorGUIUtility.standardVerticalSpacing) * 2;
				pos.x = rect.x + rect.width - right;
				pos.y = cachedY - Mathf.Ceil(EditorGUIUtility.singleLineHeight * 1.5f) - EditorGUIUtility.standardVerticalSpacing;
				pos.width = right;
				bool isDirEditManually = EditorGUI.ToggleLeft(pos, "Edit Manually", mDirManualEdit);
				if (isDirEditManually != mDirManualEdit) {
					mDirManualEdit = isDirEditManually;
					if (!mDirManualEdit) { UpdateDirIndices(mFolders); }
				}
				pos.x = rect.x;
				pos.y = cachedY;
				pos.width = rect.width - right_space;
				pos.height = EditorGUIUtility.singleLineHeight;
				mSetting.name_space = EditorGUI.TextField(pos, "NameSpace", mSetting.name_space);
				cachedY = pos.y + pos.height + EditorGUIUtility.standardVerticalSpacing;
				pos.x = rect.x;
				pos.y = cachedY;
				pos.width = (rect.width - right_space) * 0.5f;
				pos.height = EditorGUIUtility.singleLineHeight;
				mSetting.use_hash_string = EditorGUI.ToggleLeft(pos, "Use Hash String", mSetting.use_hash_string);
				pos.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				mSetting.hide_asset_properties = EditorGUI.ToggleLeft(pos, "Hide Asset Properties", mSetting.hide_asset_properties);
				pos.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				mSetting.use_public_items_getter = EditorGUI.ToggleLeft(pos, "Public Items Getter", mSetting.use_public_items_getter);
				pos.x += pos.width;
				pos.y = cachedY;
				mSetting.compress_color_into_int = EditorGUI.ToggleLeft(pos, "Compress Color into Integer", mSetting.compress_color_into_int);
				pos.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				mSetting.treat_unknown_types_as_enum = EditorGUI.ToggleLeft(pos, "Treat Unknown Types as Enum", mSetting.treat_unknown_types_as_enum);
				pos.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				mSetting.generate_tostring_method = EditorGUI.ToggleLeft(pos, "Generate ToString Method", mSetting.generate_tostring_method);
				pos.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				pos.x = rect.x;
				pos.width = rect.width;
				pos.height = mSlavesList.GetHeight();
				mSlavesList.DoList(pos);
				if (evt.type == EventType.MouseDown && evt.button == 1) {
					if (rect.Contains(evt.mousePosition) && !pos.Contains(evt.mousePosition)) {
						GenericMenu menu = new GenericMenu();
						menu.AddItem(new GUIContent("Delete"), false, () => {
							mOnRemove(this);
						});
						menu.ShowAsContext();
						evt.Use();
					}
				}
			}
		}

		private class ExcelToScriptableObjectSlaveWrap {
			public ExcelToScriptableObjectSlave slave;
			public string filterName;
			private ExcelToScriptableObjectSetting mSetting;
			private bool mDirManualEdit;
			private int mAssetDirIndex;
			private string[] mFolders;
			public ExcelToScriptableObjectSlaveWrap(ExcelToScriptableObjectSetting setting, ExcelToScriptableObjectSlave slave) {
				mSetting = setting;
				this.slave = slave;
			}
			public void UpdateDirIndices(string[] folders) {
				mFolders = folders;
				mAssetDirIndex = -1;
				for (int i = 0, imax = folders.Length; i < imax; i++) {
					if (slave.asset_directory == folders[i]) {
						mAssetDirIndex = i;
						break;
					}
				}
			}
			public float GetGUIHeight() {
				return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2 + 2f;
			}
			public void DrawGUI(Rect rect, bool isActive, bool isFocus, ToProcess toProcess) {
				Event evt = Event.current;
				Rect pos = rect;
				pos.y += 2f;
				pos.height = EditorGUIUtility.singleLineHeight;
				pos.width = rect.width - right_space - 80f;
				bool excelInvalid = string.IsNullOrEmpty(slave.excel_name);
				EditorGUI.LabelField(pos, "Excel File", excelInvalid ? "(Not Selected)" : (string.IsNullOrEmpty(filterName) ? slave.excel_name : filterName), style_rich_text);
				Color cachedGUIBGColor = GUI.backgroundColor;
				if (excelInvalid) { GUI.backgroundColor = Color.green; }
				pos.x += pos.width;
				pos.width = 40f;
				if (GUI.Button(pos, "Select", EditorStyles.miniButton)) {
					string p = EditorUtility.OpenFilePanel("Select Excel File", ".", "xlsx");
					if (!string.IsNullOrEmpty(p)) {
						string projPath = Application.dataPath;
						projPath = projPath.Substring(0, projPath.Length - 6);
						if (p.StartsWith(projPath)) { p = p.Substring(projPath.Length, p.Length - projPath.Length); }
						slave.excel_name = p;
						GUI.changed = true;
					}
				}
				GUI.backgroundColor = cachedGUIBGColor;
				if (!excelInvalid) { GUI.backgroundColor = Color.green; }
				EditorGUI.BeginDisabledGroup(excelInvalid);
				pos.x += pos.width;
				if (GUI.Button(pos, "Open", EditorStyles.miniButton)) {
					if (evt.shift) {
						string folder = Path.GetDirectoryName(slave.excel_name);
						EditorUtility.OpenWithDefaultApp(folder);
					} else {
						EditorUtility.OpenWithDefaultApp(slave.excel_name);
					}
				}
				EditorGUI.EndDisabledGroup();
				GUI.backgroundColor = cachedGUIBGColor;
				pos.x = rect.x + rect.width - right;
				pos.width = right;
				bool flushable = !string.IsNullOrEmpty(slave.excel_name) && CheckIsDirectoryValid(slave.asset_directory);
				cachedGUIBGColor = GUI.backgroundColor;
				GUI.backgroundColor = flushable ? Color.green : Color.white;
				EditorGUI.BeginDisabledGroup(!flushable);
				if (GUI.Button(pos, "Flush Data")) {
					FlushDataSettings setting = GetFlushDataSettings(mSetting);
					setting.excel_path = slave.excel_name;
					setting.asset_directory = slave.asset_directory;
					toProcess.to_flush_data.Add(setting);
				}
				GUI.backgroundColor = cachedGUIBGColor;
				EditorGUI.EndDisabledGroup();
				pos.x = rect.x;
				pos.y += pos.height + EditorGUIUtility.standardVerticalSpacing;
				pos.width = rect.width - right_space;
				pos.height = EditorGUIUtility.singleLineHeight;
				if (mDirManualEdit) {
					cachedGUIBGColor = GUI.backgroundColor;
					GUI.backgroundColor = CheckIsDirectoryValid(slave.asset_directory) ? Color.white : Color.red;
					string assetDirectory = EditorGUI.TextField(pos, "Asset Directory", slave.asset_directory);
					if (slave.asset_directory != assetDirectory) {
						slave.asset_directory = assetDirectory.Replace('\\', '/');
					}
					GUI.backgroundColor = cachedGUIBGColor;
				} else {
					int assetIndex = EditorGUI.Popup(pos, "Asset Directory", mAssetDirIndex, mFolders);
					if (assetIndex != mAssetDirIndex) {
						mAssetDirIndex = assetIndex;
						slave.asset_directory = mFolders[assetIndex];
					}
				}
				if (evt.type == EventType.MouseDown && evt.button == 0) {
					Rect posAsset = pos;
					posAsset.width = 120f;
					if (evt.clickCount == 2) {
						if (posAsset.Contains(evt.mousePosition)) {
							PingObject(slave.asset_directory, slave.excel_name, ".asset");
							evt.Use();
						}
					}
				}
				pos.x = rect.x + rect.width - right;
				pos.width = right;
				bool isDirEditManually = EditorGUI.ToggleLeft(pos, "Edit Manually", mDirManualEdit);
				if (isDirEditManually != mDirManualEdit) {
					mDirManualEdit = isDirEditManually;
					if (!mDirManualEdit) { UpdateDirIndices(mFolders); }
				}
			}
		}

		private string[] mFolders;

		private bool mToWriteAssets = false;

		private ReorderableList mSettingsDrawList;
		private List<ExcelToScriptableObjectSettingWrap> mSettingsDataList;
		private bool mSettingsDirty = false;

		private Action<ExcelToScriptableObjectSettingWrap> mOnRemoveExcel;

		void OnFocus() {
			if (mOnRemoveExcel == null) { mOnRemoveExcel = OnRemoveExcel; }
			ReadsSettings();
			List<string> folders = new List<string>();
			Queue<string> toCheckFolders = new Queue<string>();
			toCheckFolders.Enqueue("Assets");
			while (toCheckFolders.Count > 0) {
				string folder = toCheckFolders.Dequeue();
				folders.Add(folder);
				string[] subFolders = AssetDatabase.GetSubFolders(folder);
				for (int i = 0, imax = subFolders.Length; i < imax; i++) {
					toCheckFolders.Enqueue(subFolders[i].Replace('\\', '/'));
				}
			}
			mFolders = folders.ToArray();
			if (mSettingsDataList == null) {
				mSettingsDataList = new List<ExcelToScriptableObjectSettingWrap>();
			}
			int n = excel_settings.Count;
			int m = mSettingsDataList.Count;
			for (int i = 0; i < n; i++) {
				ExcelToScriptableObjectSetting setting = excel_settings[i];
				string key = setting.excel_name;
				bool flag = false;
				for (int j = i; j < m; j++) {
					ExcelToScriptableObjectSettingWrap wrap = mSettingsDataList[j];
					if (wrap.setting.excel_name == key) {
						wrap.setting = setting;
						flag = true;
						if (i != j) {
							mSettingsDataList.RemoveAt(j);
							mSettingsDataList.Insert(i, wrap);
						}
						break;
					}
				}
				if (!flag) {
					ExcelToScriptableObjectSettingWrap wrap = new ExcelToScriptableObjectSettingWrap(setting, mOnRemoveExcel);
					wrap.UpdateDirIndices(mFolders);
					mSettingsDataList.Insert(i, wrap);
				}
			}
			for (int i = m - 1; i >= n; i--) { mSettingsDataList.RemoveAt(i); }
			for (int i = 0; i < n; i++) {
				mSettingsDataList[i].UpdateDirIndices(mFolders);
			}
			if (mSettingsDrawList == null) {
				mSettingsDrawList = new ReorderableList(mSettingsDataList, typeof(ExcelToScriptableObjectSettingWrap));
				mSettingsDrawList.headerHeight = -1f;
				mSettingsDrawList.onAddCallback = (ReorderableList list) => {
					ExcelToScriptableObjectSetting setting = new ExcelToScriptableObjectSetting();
					if (mSettingsDataList.Count > 0) {
						ExcelToScriptableObjectSetting prev = mSettingsDataList[mSettingsDataList.Count - 1].setting;
						setting.script_directory = prev.script_directory;
						setting.asset_directory = prev.asset_directory;
						setting.name_space = prev.name_space;
						setting.use_hash_string = prev.use_hash_string;
						setting.hide_asset_properties = prev.hide_asset_properties;
						setting.use_public_items_getter = prev.use_public_items_getter;
						setting.compress_color_into_int = prev.compress_color_into_int;
						setting.treat_unknown_types_as_enum = prev.treat_unknown_types_as_enum;
						setting.generate_tostring_method = prev.generate_tostring_method;
					}
					ExcelToScriptableObjectSettingWrap wrap = new ExcelToScriptableObjectSettingWrap(setting, mOnRemoveExcel);
					wrap.UpdateDirIndices(mFolders);
					mSettingsDataList.Add(wrap);
				};
				mSettingsDrawList.drawNoneElementCallback = (Rect rect) => {
					Color cachedBGColor = GUI.backgroundColor;
					GUI.backgroundColor = Color.green;
					if (GUI.Button(rect, "Add New Excel Setting")) {
						mSettingsDrawList.onAddCallback(mSettingsDrawList);
					}
					GUI.backgroundColor = cachedBGColor;
				};
				mSettingsDrawList.elementHeightCallback = (int index) => {
					return mSettingsDataList[index].GetGUIHeight();
				};
				mSettingsDrawList.drawElementBackgroundCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
					if (mSettingsDataList.Count <= 0) { return; }
					Color color = new Color(0f, 0f, 0f, 0.5f - 0.15f * (index & 1));
					if (isActive && isFocused) {
						color = new Color32(61, 96, 145, 255);
					}
					rect.x += 1f;
					rect.y += 1f;
					rect.height -= 1f;
					rect.width -= 3f;
					EditorGUI.DrawRect(rect, color);
				};
				mSettingsDrawList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
					mSettingsDataList[index].DrawGUI(rect, isActive, isFocused, mToProcess);
				};
			}
			FilterSettings();
		}

		void Update() {
			if (mToWriteAssets && !EditorApplication.isCompiling) {
				mToWriteAssets = false;
				string[] jsons = EditorPrefs.GetString("excel_to_scriptableobject", "").Split(new string[] { "*#*" }, StringSplitOptions.RemoveEmptyEntries);
				EditorPrefs.DeleteKey("excel_to_scriptableobject");
				for (int i = 0, imax = jsons.Length; i < imax; i++) {
					FlushData(JsonUtility.FromJson<FlushDataSettings>(jsons[i]));
				}
				AssetDatabase.SaveAssets();
			}
		}

		private void OnRemoveExcel(ExcelToScriptableObjectSettingWrap wrap) {
			mSettingsDirty = mSettingsDataList.Remove(wrap);
			if (!string.IsNullOrEmpty(mFilter)) { FilterSettings(); }
			Repaint();
		}

		private static bool gui_inited = false;
		private static GUIStyle style_toolbar_search_text;
		private static GUIStyle style_toolbar_search_cancel;
		private static GUIStyle style_rich_text;

		private static SortedList<int, int> s_temp_sort = new SortedList<int, int>();
		private static List<MatchSegment> s_matches = new List<MatchSegment>();

		private ToProcess mToProcess = new ToProcess();
		private Vector2 mScroll = Vector2.zero;
		private string mFilter = "";
		private List<int> mSortedIndices = new List<int>();

		void OnGUI() {
			if (!gui_inited) {
				gui_inited = true;
				style_toolbar_search_text = "ToolbarSeachTextField";
				style_toolbar_search_cancel = "ToolbarSeachCancelButton";
				style_rich_text = new GUIStyle(EditorStyles.label);
				style_rich_text.richText = true;
			}
			EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);
			GUILayout.Space(4f);
			EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(10f));
			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Global Settings :");
			EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(10f));
			GUILayout.Space(12f);
			EditorGUILayout.BeginVertical();
			global_configs.field_row = EditorGUILayout.IntField("Field Row (0 based)", global_configs.field_row);
			global_configs.type_row = EditorGUILayout.IntField("Type Row (0 based)", global_configs.type_row);
			global_configs.data_from_row = EditorGUILayout.IntField("Data From Row (0 based)", global_configs.data_from_row);
			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
			GUILayout.Space(108f);
			EditorGUILayout.BeginVertical(GUILayout.Width(100f));
			GUILayout.FlexibleSpace();
			Color cachedGUIBGColor = GUI.backgroundColor;
			GUI.backgroundColor = Color.green;
			if (GUILayout.Button("Process All", GUILayout.Width(100f), GUILayout.Height(30f))) {
				for (int i = 0, imax = mSettingsDataList.Count; i < imax; i++) {
					ExcelToScriptableObjectSettingWrap wrap = mSettingsDataList[i];
					if (CheckProcessable(wrap.setting)) {
						FlushDataSettings setting = GetFlushDataSettings(wrap.setting);
						mToProcess.to_generate_code.Add(GetGenerateCodeSetting(wrap.setting));
						mToProcess.to_flush_data.Add(setting);
						for (int j = 0, jmax = wrap.setting.slaves.Length; j < jmax; j++) {
							ExcelToScriptableObjectSlave slave = wrap.setting.slaves[j];
							setting.excel_path = slave.excel_name;
							setting.asset_directory = slave.asset_directory;
							mToProcess.to_flush_data.Add(setting);
						}
					}
				}
			}
			GUILayout.FlexibleSpace();
			GUI.backgroundColor = cachedGUIBGColor;
			EditorGUILayout.EndVertical();
			GUILayout.Space(8f);
			EditorGUILayout.EndHorizontal();
			GUILayout.Space(4f);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Excel Settings :");
			bool filterChanged = false;
			string filter = GUILayout.TextField(mFilter, style_toolbar_search_text, GUILayout.Width(200f));
			if (GUILayout.Button(GUIContent.none, style_toolbar_search_cancel)) {
				filter = "";
			}
			if (filter != mFilter) {
				mFilter = filter;
				filterChanged = true;
				FilterSettings();
			}
			EditorGUILayout.EndHorizontal();
			GUILayout.Space(4f);
			mScroll = EditorGUILayout.BeginScrollView(mScroll, false, false);
			if (string.IsNullOrEmpty(mFilter)) {
				mSettingsDrawList.DoLayoutList();
			} else {
				for (int i = 0, imax = mSortedIndices.Count; i < imax; i++) {
					int index = mSortedIndices[i];
					ExcelToScriptableObjectSettingWrap wrap = mSettingsDataList[index];
					float height = wrap.GetGUIHeight();
					Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Height(height));
					mSettingsDrawList.drawElementBackgroundCallback(rect, index, false, false);
					rect.x += 16f;
					rect.width -= 18f;
					wrap.DrawGUI(rect, false, false, mToProcess);
				}
			}
			EditorGUILayout.EndScrollView();
			GUILayout.Space(4f);
			EditorGUI.EndDisabledGroup();
			if ((GUI.changed && !filterChanged) || mSettingsDirty) {
				mSettingsDirty = false;
				int n = mSettingsDataList.Count;
				excel_settings.Clear();
				for (int i = 0; i < n; i++) {
					excel_settings.Add(mSettingsDataList[i].setting);
				}
				WriteSettings();
			}
			if (mToProcess.to_flush_data.Count > 0) {
				int n = mToProcess.to_flush_data.Count;
				string[] jsons = new string[n];
				for (int i = 0; i < n; i++) {
					jsons[i] = JsonUtility.ToJson(mToProcess.to_flush_data[i], false);
				}
				mToProcess.to_flush_data.Clear();
				EditorPrefs.SetString("excel_to_scriptableobject", string.Join("*#*", jsons));
				mToWriteAssets = true;
			}
			if (mToProcess.to_generate_code.Count > 0) {
				for (int i = 0, imax = mToProcess.to_generate_code.Count; i < imax; i++) {
					GenerateCode(mToProcess.to_generate_code[i]);
				}
				mToProcess.to_generate_code.Clear();
				AssetDatabase.Refresh();
			}
		}

		private void FilterSettings() {
			mSortedIndices.Clear();
			if (string.IsNullOrEmpty(mFilter)) {
				for (int i = 0, imax = mSettingsDataList.Count; i < imax; i++) {
					ExcelToScriptableObjectSettingWrap we = mSettingsDataList[i];
					we.filterName = null;
					for (int j = we.slaves.Count - 1; j >= 0; j--) {
						ExcelToScriptableObjectSlaveWrap ws = we.slaves[j];
						ws.filterName = null;
					}
				}
			} else {
				s_temp_sort.Clear();
				string f = mFilter.ToLower();
				for (int i = 0, imax = mSettingsDataList.Count; i < imax; i++) {
					ExcelToScriptableObjectSettingWrap we = mSettingsDataList[i];
					we.filterName = null;
					int min = int.MaxValue;
					s_matches.Clear();
					int steps, breaks;
					if (StringChangeOperations(f, we.setting.excel_name.ToLower(), s_matches, out steps, out breaks)) {
						int ops = steps + breaks - we.setting.excel_name.Length;
						min = Mathf.Min(min, ops);
						we.filterName = HighlightMatches(we.setting.excel_name, s_matches);
					}
					for (int j = we.slaves.Count - 1; j >= 0; j--) {
						ExcelToScriptableObjectSlaveWrap ws = we.slaves[j];
						s_matches.Clear();
						ws.filterName = null;
						if (StringChangeOperations(f, ws.slave.excel_name.ToLower(), s_matches, out steps, out breaks)) {
							int ops = steps + breaks - ws.slave.excel_name.Length;
							min = Mathf.Min(min, ops);
							ws.filterName = HighlightMatches(ws.slave.excel_name, s_matches);
						}
					}
					if (min < int.MaxValue) { s_temp_sort.Add((min << 10) + i, i); }
				}
				foreach (KeyValuePair<int, int> kv in s_temp_sort) {
					mSortedIndices.Add(kv.Value);
				}
			}
		}

		static GenerateCodeSettings GetGenerateCodeSetting(ExcelToScriptableObjectSetting setting) {
			GenerateCodeSettings ret = new GenerateCodeSettings();
			ret.excel_path = setting.excel_name;
			ret.script_directory = setting.script_directory;
			ret.name_space = setting.name_space;
			ret.use_hash_string = setting.use_hash_string;
			ret.hide_asset_properties = setting.hide_asset_properties;
			ret.use_public_items_getter = setting.use_public_items_getter;
			ret.compress_color_into_int = setting.compress_color_into_int;
			ret.treat_unknown_types_as_enum = setting.treat_unknown_types_as_enum;
			ret.generate_tostring_method = setting.generate_tostring_method;
			return ret;
		}

		static FlushDataSettings GetFlushDataSettings(ExcelToScriptableObjectSetting setting) {
			FlushDataSettings ret = new FlushDataSettings();
			string className = Path.GetFileNameWithoutExtension(setting.excel_name);
			ret.excel_path = setting.excel_name;
			ret.asset_directory = setting.asset_directory;
			ret.class_name = string.IsNullOrEmpty(setting.name_space) ? className : (setting.name_space + "." + className);
			ret.use_hash_string = setting.use_hash_string;
			ret.compress_color_into_int = setting.compress_color_into_int;
			ret.treat_unknown_types_as_enum = setting.treat_unknown_types_as_enum;
			return ret;
		}

		private struct MatchSegment {
			public int index;
			public int length;
		}

		static bool StringChangeOperations(string from, string to, IList<MatchSegment> matches, out int steps, out int breaks) {
			int ignores = 0;
			int best = int.MaxValue;
			int bestIgnore = -1;
			while (true) {
				int _steps;
				int _breaks;
				if (!TryStringChangeOperations(from, to, ignores, null, out _steps, out _breaks)) {
					break;
				}
				int score = _steps + _breaks;
				if (score < best) { best = score; bestIgnore = ignores; }
				ignores++;
			}
			if (bestIgnore < 0) { steps = 0; breaks = 0; return false; }
			return TryStringChangeOperations(from, to, bestIgnore, matches, out steps, out breaks);
		}

		private static bool TryStringChangeOperations(string from, string to, int ignores, IList<MatchSegment> matches, out int steps, out int breaks) {
			steps = 0;
			breaks = 0;
			int lenF = from.Length;
			int lenT = to.Length;
			int fi = 0;
			int ti = 0;
			int matching = -1;
			while (fi < lenF && ti < lenT) {
				bool match = from[fi] == to[ti];
				if (match) {
					if (ignores > 0) {
						ignores--;
						match = false;
					}
				}
				if (match) {
					if (matching < 0) {
						matching = ti;
						if (fi > 0) { breaks++; }
					}
					fi++;
					ti++;
				} else {
					if (matches != null && matching >= 0) {
						matches.Add(new MatchSegment() { index = matching, length = ti - matching });
					}
					ti++;
					steps++;
					matching = -1;
				}
			}
			if (matches != null && matching >= 0) {
				matches.Add(new MatchSegment() { index = matching, length = ti - matching });
			}
			if (ti < lenT) {
				steps += lenT - ti;
				breaks++;
			}
			return fi == lenF;
		}

		private static string HighlightMatches(string content, List<MatchSegment> matches) {
			for (int i = matches.Count - 1; i >= 0; i--) {
				MatchSegment seg = matches[i];
				string pre = content.Substring(0, seg.index);
				string mat = content.Substring(seg.index, seg.length);
				string suf = content.Substring(seg.index + seg.length);
				content = pre + "<color=yellow><b>" + mat + "</b></color>" + suf;
			}
			return content;
		}

		private static void PingObject(string dir, string excel, string ext) {
			string fn = Path.GetFileNameWithoutExtension(excel);
			string path = dir + (dir.EndsWith("/") ? "" : "/") + fn + ext;
			UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
			if (obj == null) {
				string dp = dir.EndsWith("/") ? dir.Substring(0, dir.Length - 1) : dir;
				UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath(dp, typeof(UnityEngine.Object));
				if (folder != null) { EditorGUIUtility.PingObject(folder); }
			} else {
				EditorGUIUtility.PingObject(obj);
			}
		}

	}

	[Serializable]
	public class ExcelToScriptableObjectSettings {
		public ExcelToScriptableObjectGlobalConfigs configs;
		public ExcelToScriptableObjectSetting[] excels;
	}

	[Serializable]
	public class ExcelToScriptableObjectGlobalConfigs {
		public int field_row = 0;
		public int type_row = 1;
		public int data_from_row = 2;
	}

	[Serializable]
	public class ExcelToScriptableObjectSetting {
		public string excel_name;
		public string script_directory = "Assets";
		public string asset_directory = "Assets";
		public string name_space;
		public bool use_hash_string = false;
		public bool hide_asset_properties = true;
		public bool use_public_items_getter = false;
		public bool compress_color_into_int = true;
		public bool treat_unknown_types_as_enum = false;
		public bool generate_tostring_method = true;
		public ExcelToScriptableObjectSlave[] slaves;
	}

	[Serializable]
	public class ExcelToScriptableObjectSlave {
		public string excel_name;
		public string asset_directory = "Assets";
	}

}
