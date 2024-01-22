using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GreatClock.Common.ExcelToSO {

	/// <summary>
	/// Attribute for specifing a field filter for specific tables.<br />
	/// The static method should batch : <br />
	/// <i>string YourFilterMethod(string tableName, string fieldName[, string fieldContentInRow])</i><br />
	/// The return value of your method is the field name that will be used in your script and data asset.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public class ExcelFieldFilterAttribute : Attribute {
		public readonly eMatchType MatchType;
		public readonly string TableName;
		public readonly int Priority;
		public readonly int RequireRowIndex;
		/// <summary>
		/// Mark the static method as a <b>Filter</b> for <b>field</b> in sheet. The sheet is specified by parameter tableName.<br />
		/// The static method should batch : <br />
		/// <i>string YourFilterMethod(string tableName, string fieldName[, string fieldContentInRow])</i><br />
		/// The return value of your method is the field name that will be used in your script and data asset.
		/// </summary>
		/// <param name="matchType">How to match specific sheets, Full Match or Regex Match.</param>
		/// <param name="tableName">The name of sheet.</param>
		/// <param name="priority">The priority when selecting a filter when more than one filters that match. The bigger the higher.</param>
		/// <param name="requireRowIndex">The additional row index of the field column for filtering.</param>
		public ExcelFieldFilterAttribute(eMatchType matchType, string tableName, int priority, int requireRowIndex = -1) {
			MatchType = matchType;
			TableName = tableName;
			Priority = priority;
			RequireRowIndex = requireRowIndex;
		}
	}

	public enum eMatchType { FullMatch, Regex }

	internal static class  ExcelFieldFilterManager
	{

		private delegate string FilterDelegate3(string p1, string p2, string p3);
		private delegate string FilterDelegate2(string p1, string p2);

		private class FilterData {
			private ExcelFieldFilterAttribute mAttr;
			private FilterDelegate3 mFilter3;
			private FilterDelegate2 mFilter2;
			public FilterData(ExcelFieldFilterAttribute attr, Delegate func) {
				mAttr = attr;
				mFilter3 = func as FilterDelegate3;
				mFilter2 = func as FilterDelegate2;
			}
			public int Priority { get { return mAttr.Priority; } }
			public bool Match(string tableName) {
				switch (mAttr.MatchType) {
					case eMatchType.Regex:
						if (Regex.IsMatch(tableName, mAttr.TableName)) { return true; }
						break;
					default:
						if (tableName == mAttr.TableName) { return true; }
						break;
				}
				return false;
			}
			public string Filter(string tableName, string fieldName, Func<int, string> valuePicker) {
				if (mFilter3 != null) {
					string val = valuePicker(mAttr.RequireRowIndex);
					return mFilter3(tableName, fieldName, val);
				} else if (mFilter2 != null) {
					return mFilter2(tableName, fieldName);
				}
				return fieldName;
			}
		}
		private static List<FilterData> s_filters = null;

		internal static string FilterField(string tableName, string fieldName, Func<int, string> valuePicker) {
			if (s_filters == null) {
				s_filters = new List<FilterData>();
				Type del3 = typeof(FilterDelegate3);
				Type del2 = typeof(FilterDelegate2);
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					foreach (Type type in assembly.GetTypes()) {
						foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
							foreach (ExcelFieldFilterAttribute attr in method.GetCustomAttributes<ExcelFieldFilterAttribute>(false)) {
								if (!method.IsStatic) {
									Debug.LogError("[ExcelToScriptableObject] 'ExcelFieldFilterAttribute' can only be used on static methods !");
									break;
								}
								Delegate func = null;
								try {
									switch (method.GetParameters().Length) {
										case 2:
											func = method.CreateDelegate(del2);
											break;
										case 3:
											func = method.CreateDelegate(del3);
											break;
									}
								} catch {
									Debug.LogErrorFormat("[ExcelToScriptableObject] Excel Field Filter method should be in format 'bool YourFilterMethod(string tableName, string fieldName[, string fieldContentInRow])' in ({0}.{1}).",
										type.FullName, method.Name);
								}
								s_filters.Add(new FilterData(attr, func));
							}
						}
					}
				}
			}
			FilterData filter = null;
			for (int i = s_filters.Count - 1; i >= 0; i--) {
				FilterData f = s_filters[i];
				if (!f.Match(tableName)) { continue; }
				if (filter == null || f.Priority > filter.Priority) {
					filter = f;
				}
			}
			if (filter == null) { return fieldName; }
			return filter.Filter(tableName, fieldName, valuePicker);
		}
	}

}
