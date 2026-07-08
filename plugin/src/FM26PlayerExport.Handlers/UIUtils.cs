using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine.UIElements;

namespace FM26PlayerExport.Handlers;

public static class UIUtils
{
	public static string GameLang = "pt";

	public static string GetTrans(string key)
	{
		if (GameLang == "en")
		{
			if (key == "Amarelo")
			{
				return "Yellow";
			}
			if (key == "Vermelho")
			{
				return "Red";
			}
			if (key == "Sub In")
			{
				return "Sub In";
			}
			if (key == "Sub Out")
			{
				return "Sub Out";
			}
			if (key == "Lesão")
			{
				return "Injured";
			}
		}
		else if (GameLang == "es")
		{
			if (key == "Amarelo")
			{
				return "Amarilla";
			}
			if (key == "Vermelho")
			{
				return "Roja";
			}
			if (key == "Sub In")
			{
				return "Entra";
			}
			if (key == "Sub Out")
			{
				return "Sale";
			}
			if (key == "Lesão")
			{
				return "Lesión";
			}
		}
		else
		{
			if (key == "Sub In")
			{
				return "Entra";
			}
			if (key == "Sub Out")
			{
				return "Sai";
			}
		}
		return key;
	}

	public static string GetText(VisualElement el)
	{
		if (el == null)
		{
			return null;
		}
		try
		{
			TextElement val = ((Il2CppObjectBase)el).TryCast<TextElement>();
			if (val != null && !string.IsNullOrWhiteSpace(val.text))
			{
				return StripHtml(val.text.Trim());
			}
		}
		catch
		{
		}
		// FM26's SIText declares its own `text` property (hides TextElement.text, which stays empty).
		try
		{
			SI.Bindable.SIText st = ((Il2CppObjectBase)el).TryCast<SI.Bindable.SIText>();
			if (st != null && !string.IsNullOrWhiteSpace(st.text))
			{
				return StripHtml(st.text.Trim());
			}
		}
		catch
		{
		}
		// Last resort: the string UITK actually rendered.
		try
		{
			TextElement te = ((Il2CppObjectBase)el).TryCast<TextElement>();
			if (te != null && !string.IsNullOrWhiteSpace(te.m_RenderedText))
			{
				return StripHtml(te.m_RenderedText.Trim());
			}
		}
		catch
		{
		}
		try
		{
			Label val2 = ((Il2CppObjectBase)el).TryCast<Label>();
			if (val2 != null && !string.IsNullOrWhiteSpace(((TextElement)val2).text))
			{
				return StripHtml(((TextElement)val2).text.Trim());
			}
		}
		catch
		{
		}
		try
		{
			string tooltip = el.tooltip;
			if (!string.IsNullOrWhiteSpace(tooltip))
			{
				return StripHtml(tooltip.Trim());
			}
		}
		catch
		{
		}
		return null;
	}

	public static string StripHtml(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return Regex.Replace(s, "<[^>]+>", string.Empty).Trim();
		}
		return s;
	}

	public static string CollectFirstText(VisualElement el, int d = 0)
	{
		if (el == null || d > 20)
		{
			return null;
		}
		string text = GetText(el);
		if (text != null)
		{
			return text;
		}
		for (int i = 0; i < el.childCount; i++)
		{
			string text2 = CollectFirstText(el.ElementAt(i), d + 1);
			if (text2 != null)
			{
				return text2;
			}
		}
		return null;
	}

	public static string CollectFirstTooltip(VisualElement el, int d = 0)
	{
		if (el == null || d > 20)
		{
			return null;
		}
		try
		{
			string tooltip = el.tooltip;
			if (!string.IsNullOrWhiteSpace(tooltip))
			{
				return StripHtml(tooltip.Trim());
			}
		}
		catch
		{
		}
		for (int i = 0; i < el.childCount; i++)
		{
			string text = CollectFirstTooltip(el.ElementAt(i), d + 1);
			if (text != null)
			{
				return text;
			}
		}
		return null;
	}

	public static string CollectAllTextsJoined(VisualElement el, int d = 0)
	{
		if (el == null || d > 20)
		{
			return "";
		}
		List<string> val = new List<string>();
		CollectAllTexts(el, val);
		return string.Join(" ", (global::System.Collections.Generic.IEnumerable<string>)val).Trim();
	}

	public static void CollectAllTexts(VisualElement el, List<string> out_, int d = 0)
	{
		if (el == null || d > 20)
		{
			return;
		}
		string text = GetText(el);
		if (text != null && !out_.Contains(text))
		{
			out_.Add(text);
		}
		try
		{
			string tooltip = el.tooltip;
			if (!string.IsNullOrWhiteSpace(tooltip))
			{
				string text2 = StripHtml(tooltip.Trim());
				if (!out_.Contains(text2))
				{
					out_.Add(text2);
				}
			}
		}
		catch
		{
		}
		for (int i = 0; i < el.childCount; i++)
		{
			CollectAllTexts(el.ElementAt(i), out_, d + 1);
		}
	}

	public static string TryReadStars(VisualElement cell)
	{
		try
		{
			string tooltip = cell.tooltip;
			double num = default(double);
			if (!string.IsNullOrEmpty(tooltip) && double.TryParse(tooltip, out num))
			{
				return tooltip;
			}
		}
		catch
		{
		}
		int filled = 0;
		int half = 0;
		int total = 0;
		CountStars(cell, ref filled, ref half, ref total, 0);
		if (total == 0)
		{
			return null;
		}
		float num2 = (float)filled + (float)half * 0.5f;
		if (num2 <= 0f)
		{
			return string.Empty;
		}
		return num2.ToString("0.#", (IFormatProvider)(object)CultureInfo.InvariantCulture).Replace(".", ",");
	}

	private static void CountStars(VisualElement el, ref int filled, ref int half, ref int total, int d)
	{
		if (el == null || d > 12)
		{
			return;
		}
		try
		{
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			for (int i = 0; i < el.classList.Count; i++)
			{
				string text = el.classList[i].ToLower();
				if (text.Contains("star") || text.Contains("ability") || text.Contains("rating"))
				{
					flag = true;
				}
				if (text.Contains("filled") || text.Contains("active") || text.Contains("full") || text.Contains("on"))
				{
					flag2 = true;
				}
				if (text.Contains("half"))
				{
					flag3 = true;
				}
			}
			if (flag && el.childCount == 0)
			{
				total++;
				if (flag3)
				{
					half++;
				}
				else if (flag2)
				{
					filled++;
				}
			}
		}
		catch
		{
		}
		for (int j = 0; j < el.childCount; j++)
		{
			CountStars(el.ElementAt(j), ref filled, ref half, ref total, d + 1);
		}
	}

	public static string RowKey(List<string> vals)
	{
		if (vals == null || vals.Count == 0)
		{
			return string.Empty;
		}
		return string.Join("|", (global::System.Collections.Generic.IEnumerable<string>)vals);
	}

	public static VisualElement FindByName(VisualElement el, string name)
	{
		if (el == null)
		{
			return null;
		}
		if (el.name == name)
		{
			return el;
		}
		for (int i = 0; i < el.childCount; i++)
		{
			VisualElement val = FindByName(el.ElementAt(i), name);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	public static string Esc(string v)
	{
		if (string.IsNullOrEmpty(v))
		{
			return string.Empty;
		}
		v = v.Replace("\r", " ").Replace("\n", " ");
		string text = new string(new char[1] { '"' });
		if (v.Contains(";") || v.Contains(text))
		{
			v = text + v.Replace(text, text + text) + text;
		}
		return v;
	}

	public static string LerIconesComoTexto(VisualElement el, int d = 0)
	{
		if (el == null || d > 6)
		{
			return "";
		}
		List<string> val = new List<string>();
		try
		{
			for (int i = 0; i < el.classList.Count; i++)
			{
				string text = el.classList[i].ToLower();
				if (text.Contains("yellow"))
				{
					val.Add(GetTrans("Amarelo"));
				}
				if (text.Contains("red"))
				{
					val.Add(GetTrans("Vermelho"));
				}
				if (text.Contains("sub") && !text.Contains("subject"))
				{
					if (text.Contains("on") || text.Contains("in"))
					{
						val.Add(GetTrans("Sub In"));
					}
					else if (text.Contains("off") || text.Contains("out"))
					{
						val.Add(GetTrans("Sub Out"));
					}
					else
					{
						val.Add("Sub");
					}
				}
				if (text.Contains("injur"))
				{
					val.Add(GetTrans("Lesão"));
				}
				if (text.Contains("condition") || text.Contains("heart") || text.Contains("sharpness"))
				{
					val.Add("Coração");
				}
				if (text.Contains("fatigue") || text.Contains("tired"))
				{
					val.Add("Fadigado");
				}
			}
			string tooltip = el.tooltip;
			if (!string.IsNullOrWhiteSpace(tooltip) && val.Count > 0)
			{
				string text2 = StripHtml(tooltip);
				if (text2.Length < 40)
				{
					string text3 = val[val.Count - 1];
					if (text3 != GetTrans("Sub In") && text3 != GetTrans("Sub Out"))
					{
						val[val.Count - 1] = text2;
					}
				}
			}
		}
		catch
		{
		}
		for (int j = 0; j < el.childCount; j++)
		{
			string text4 = LerIconesComoTexto(el.ElementAt(j), d + 1);
			if (string.IsNullOrEmpty(text4))
			{
				continue;
			}
			string[] array = text4.Split(new string[1] { " | " }, (StringSplitOptions)1);
			foreach (string text5 in array)
			{
				if (!val.Contains(text5))
				{
					val.Add(text5);
				}
			}
		}
		return string.Join(" | ", (global::System.Collections.Generic.IEnumerable<string>)val);
	}

	public static string RealTypeName(VisualElement el)
	{
		try
		{
			IntPtr klass = IL2CPP.il2cpp_object_get_class(((Il2CppObjectBase)el).Pointer);
			if (klass == IntPtr.Zero)
			{
				return "?";
			}
			string ns = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(klass)) ?? "";
			string nm = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(klass)) ?? "?";
			return string.IsNullOrEmpty(ns) ? nm : (ns + "." + nm);
		}
		catch
		{
			return "?";
		}
	}

	public static string ProbeText(VisualElement el)
	{
		StringBuilder sb = new StringBuilder();
		try
		{
			SI.Bindable.SIText st = ((Il2CppObjectBase)el).TryCast<SI.Bindable.SIText>();
			if (st == null)
			{
				sb.Append("cast=NULL ");
			}
			else
			{
				sb.Append("cast=OK ");
				try { sb.Append("text='" + (st.text ?? "<null>") + "' "); } catch (global::System.Exception e) { sb.Append("text!EX:" + e.GetType().Name + ":" + e.Message + " "); }
				try { sb.Append("finalSet=" + st.m_finalTextSet + " "); } catch (global::System.Exception e) { sb.Append("finalSet!EX:" + e.GetType().Name + " "); }
			}
		}
		catch (global::System.Exception e)
		{
			sb.Append("cast!EX:" + e.GetType().Name + ":" + e.Message + " ");
		}
		try
		{
			TextElement te = ((Il2CppObjectBase)el).TryCast<TextElement>();
			if (te == null)
			{
				sb.Append("te=NULL ");
			}
			else
			{
				try { sb.Append("m_Text='" + (te.m_Text ?? "<null>") + "' "); } catch (global::System.Exception e) { sb.Append("m_Text!EX:" + e.GetType().Name + " "); }
				try { sb.Append("rendered='" + (te.m_RenderedText ?? "<null>") + "' "); } catch (global::System.Exception e) { sb.Append("rendered!EX:" + e.GetType().Name + " "); }
				try { sb.Append("elided='" + (te.elidedText ?? "<null>") + "' "); } catch (global::System.Exception e) { sb.Append("elided!EX:" + e.GetType().Name + " "); }
			}
		}
		catch (global::System.Exception e)
		{
			sb.Append("te!EX:" + e.GetType().Name + " ");
		}
		return sb.ToString();
	}

	public static VisualElement FindLeaf(VisualElement el)
	{
		while (el != null && el.childCount > 0)
		{
			el = el.ElementAt(el.childCount - 1);
		}
		return el;
	}

	public static string DiagCell(VisualElement el, int d = 0)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		if (el == null || d > 12)
		{
			return string.Empty;
		}
		StringBuilder val = new StringBuilder();
		try
		{
			for (int i = 0; i < el.classList.Count; i++)
			{
				if (i > 0)
				{
					val.Append(',');
				}
				val.Append(el.classList[i]);
			}
		}
		catch
		{
		}
		string text = GetText(el) ?? string.Empty;
		string elName = string.Empty;
		try { elName = el.name ?? string.Empty; } catch { }
		StringBuilder val2 = new StringBuilder();
		val2.Append($"{new string('-', d)}{RealTypeName(el)}[name={elName},cls={val},ch={el.childCount},txt={text}] ");
		for (int j = 0; j < el.childCount; j++)
		{
			val2.Append(DiagCell(el.ElementAt(j), d + 1));
		}
		return ((object)val2).ToString();
	}
}
