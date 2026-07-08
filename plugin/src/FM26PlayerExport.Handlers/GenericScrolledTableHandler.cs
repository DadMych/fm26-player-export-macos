using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM26PlayerExport.Handlers;

public abstract class GenericScrolledTableHandler : IExportHandler
{
	private const int WAIT_FRAMES = 4;

	private const int ZERO_STEPS_MAX = 3;

	// Transient UI exceptions (bindings mid-update, repaint re-entrancy) shouldn't kill a
	// long capture: back off and retry, only give up after this many consecutive failures.
	private const int ERROR_RETRIES_MAX = 8;

	private int _errorRetries;

	protected VisualElement _captureView;

	protected List<string> _captureHeaders;

	private HashSet<string> _seenKeys;

	private float _lastScrollY;

	private int _scrollAttempts;

	private int _maxScrollAttempts;

	private int _maxRows;

	private int _capturedRowCount;

	private int _zeroSteps;

	private int _endSteps;

	private bool _diagLogged;

	private int _captureWait;

	private bool _isComplete;

	private bool _outputStarted;

	private bool _outputFinalized;

	private StreamWriter _csvWriter;

	private StreamWriter _htmlWriter;

	private string _csvFile;

	private string _htmlFile;

	[field: CompilerGenerated]
	protected string FilePrefix
	{
		[CompilerGenerated]
		get;
		[CompilerGenerated]
		set;
	} = "export_";

	public virtual bool TryStartCapture(VisualElement root, out string errorMessage)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Expected O, but got Unknown
		//IL_03be: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c5: Expected O, but got Unknown
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		errorMessage = string.Empty;
		VisualElement val = null;
		VisualElement val2 = null;
		VisualElement val3 = UIUtils.FindByName(root, "playertable") ?? UIUtils.FindByName(root, "client-object-viewer-table");
		if (val3 != null)
		{
			val = UIUtils.FindByName(val3, "column-headers");
			val2 = UIUtils.FindByName(val3, "View");
		}
		if (val == null || val2 == null)
		{
			List<VisualElement> val4 = new List<VisualElement>();
			FindAllByName(root, "column-headers", val4);
			var enumerator = val4.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					VisualElement current = enumerator.Current;
					object obj = UIUtils.FindByName(current.parent, "View");
					if (obj == null)
					{
						VisualElement parent = current.parent;
						obj = ((((parent != null) ? parent.parent : null) != null) ? UIUtils.FindByName(current.parent.parent, "View") : null);
						if (obj == null)
						{
							VisualElement parent2 = current.parent;
							object obj2;
							if (parent2 == null)
							{
								obj2 = null;
							}
							else
							{
								VisualElement parent3 = parent2.parent;
								obj2 = ((parent3 != null) ? parent3.parent : null);
							}
							obj = ((obj2 != null) ? UIUtils.FindByName(current.parent.parent.parent, "View") : null);
						}
					}
					VisualElement val5 = (VisualElement)obj;
					if (val5 != null && val5.childCount > 0)
					{
						val = current;
						val2 = val5;
						break;
					}
				}
			}
			finally
			{
				((global::System.IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
			}
		}
		if (val == null || val2 == null)
		{
			errorMessage = "[FM26Export.GenericTable] Nenhuma tabela de lista ('playertable' ou generica) encontrada na UI.";
			return false;
		}
		_captureView = val2;
		_captureHeaders = new List<string>();
		for (int i = 1; i < val.childCount; i++)
		{
			VisualElement el = val.ElementAt(i);
			List<string> val6 = new List<string>();
			UIUtils.CollectAllTexts(el, val6);
			string text = string.Empty;
			var enumerator2 = val6.GetEnumerator();
			try
			{
				while (enumerator2.MoveNext())
				{
					string current2 = enumerator2.Current;
					string text2 = current2.ToLowerInvariant();
					if (!text2.Contains("sort") && !text2.Contains("orden") && !text2.Contains("order") && current2.Length > text.Length)
					{
						text = current2;
					}
				}
			}
			finally
			{
				((global::System.IDisposable)enumerator2/*cast due to constrained. prefix*/).Dispose();
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				string text3 = UIUtils.CollectFirstTooltip(el);
				string text4 = UIUtils.CollectFirstText(el);
				text = ((!string.IsNullOrWhiteSpace(text3)) ? text3 : text4);
			}
			_captureHeaders.Add((text != null) ? UIUtils.Esc(text) : $"Col{i}");
		}
		if (_captureHeaders.Count == 0)
		{
			_captureHeaders.Add("Dados");
		}
		if (!IsValidScreen(root, _captureHeaders))
		{
			errorMessage = "[FM26Export] Tabela encontrada, mas rejeitada pelo handler generico/filho. Headers: " + string.Join(", ", (global::System.Collections.Generic.IEnumerable<string>)_captureHeaders);
			return false;
		}
		ManualLogSource log = Plugin.Log;
		bool flag = default(bool);
		BepInExInfoLogInterpolatedStringHandler val7 = new BepInExInfoLogInterpolatedStringHandler(25, 2, out flag);
		if (flag)
		{
			((BepInExLogInterpolatedStringHandler)val7).AppendLiteral("[FM26Export] Headers (");
			((BepInExLogInterpolatedStringHandler)val7).AppendFormatted<int>(_captureHeaders.Count);
			((BepInExLogInterpolatedStringHandler)val7).AppendLiteral("): ");
			((BepInExLogInterpolatedStringHandler)val7).AppendFormatted<string>(string.Join(" | ", (global::System.Collections.Generic.IEnumerable<string>)_captureHeaders));
		}
		log.LogInfo(val7);
		_seenKeys = new HashSet<string>();
		_scrollAttempts = 0;
		_maxRows = PluginConfig.EffectiveMaxRowsToExport;
		_maxScrollAttempts = PluginConfig.GetMaxScrollAttemptsForRows(_maxRows);
		_capturedRowCount = 0;
		_zeroSteps = 0;
		_endSteps = 0;
		_lastScrollY = -1f;
		_diagLogged = false;
		_errorRetries = 0;
		_isComplete = false;
		_outputStarted = false;
		_outputFinalized = false;
		_csvWriter = null;
		_htmlWriter = null;
		_csvFile = null;
		_htmlFile = null;
		ScrollView firstAncestorOfType = _captureView.GetFirstAncestorOfType<ScrollView>();
		if (firstAncestorOfType != null)
		{
			firstAncestorOfType.scrollOffset = Vector2.zero;
		}
		_captureWait = PluginConfig.EffectiveScrollStepDelayFrames;
		ManualLogSource log2 = Plugin.Log;
		val7 = new BepInExInfoLogInterpolatedStringHandler(75, 3, out flag);
		if (flag)
		{
			((BepInExLogInterpolatedStringHandler)val7).AppendLiteral("[FM26Export] Captura de lista (");
			((BepInExLogInterpolatedStringHandler)val7).AppendFormatted<string>(FilePrefix);
			((BepInExLogInterpolatedStringHandler)val7).AppendLiteral(") iniciada. Limite=");
			((BepInExLogInterpolatedStringHandler)val7).AppendFormatted<int>(_maxRows);
			((BepInExLogInterpolatedStringHandler)val7).AppendLiteral(" linhas | scroll safety=");
			((BepInExLogInterpolatedStringHandler)val7).AppendFormatted<int>(_maxScrollAttempts);
			((BepInExLogInterpolatedStringHandler)val7).AppendLiteral(".");
		}
		log2.LogInfo(val7);
		return true;
	}

	protected abstract bool IsValidScreen(VisualElement root, List<string> headers);

	public bool CaptureStep()
	{
		//IL_039a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a1: Expected O, but got Unknown
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Expected O, but got Unknown
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Expected O, but got Unknown
		//IL_0378: Unknown result type (might be due to invalid IL or missing references)
		if (_isComplete)
		{
			return true;
		}
		if (_captureWait > 0)
		{
			_captureWait--;
			return false;
		}
		bool flag3 = default(bool);
		try
		{
			int num = 0;
			for (int i = 0; i < _captureView.childCount; i++)
			{
				VisualElement val = _captureView.ElementAt(i);
				bool flag = false;
				try
				{
					flag = val.ClassListContains("virtualised-list__item--selected");
				}
				catch
				{
				}
				if (!flag)
				{
					continue;
				}
				bool flag2 = !_diagLogged && _scrollAttempts == 0 && num == 0;
				List<string> val2 = ReadRow(val, flag2, _captureHeaders);
				if (flag2)
				{
					_diagLogged = true;
				}
				if (val2.Count == 0)
				{
					continue;
				}
				string text = UIUtils.RowKey(val2);
				if (string.IsNullOrEmpty(text) || _seenKeys.Contains(text))
				{
					continue;
				}
				_seenKeys.Add(text);
				if (!WriteCapturedRow(val2))
				{
					_isComplete = true;
					return true;
				}
				num++;
				if (_capturedRowCount >= _maxRows)
				{
					ManualLogSource log = Plugin.Log;
					BepInExWarningLogInterpolatedStringHandler val3 = new BepInExWarningLogInterpolatedStringHandler(52, 1, out flag3);
					if (flag3)
					{
						((BepInExLogInterpolatedStringHandler)val3).AppendLiteral("[FM26Export] Limite configurado de ");
						((BepInExLogInterpolatedStringHandler)val3).AppendFormatted<int>(_maxRows);
						((BepInExLogInterpolatedStringHandler)val3).AppendLiteral(" linhas atingido.");
					}
					log.LogWarning(val3);
					_isComplete = true;
					return true;
				}
			}
			ScrollView firstAncestorOfType = _captureView.GetFirstAncestorOfType<ScrollView>();
			float num2 = ((firstAncestorOfType != null) ? firstAncestorOfType.scrollOffset.y : 0f);
			_scrollAttempts++;
			bool flag4 = Math.Abs(num2 - _lastScrollY) < 0.5f && _lastScrollY >= 0f;
			if (num == 0)
			{
				_zeroSteps++;
			}
			else
			{
				_zeroSteps = 0;
			}
			// The list is virtualised: the scroll offset can clamp temporarily while the
			// virtualiser extends its content, so a single "offset didn't move" is NOT the end.
			// Finish only after several consecutive steps with a pinned offset AND no new rows.
			if (flag4 && num == 0)
			{
				_endSteps++;
			}
			else
			{
				_endSteps = 0;
			}
			bool flag5 = _zeroSteps >= 12;
			Plugin.Log.LogInfo((object)("[FM26Export] Step " + _scrollAttempts + ": +" + num
				+ " | total=" + _capturedRowCount + " | scrollY=" + num2.ToString("F0")
				+ " | pinned=" + flag4 + " | end=" + _endSteps + "/3 | stall=" + _zeroSteps + "/12"));
			if (_endSteps >= 3 || _scrollAttempts >= _maxScrollAttempts || flag5)
			{
				if (flag5 && _endSteps < 3)
				{
					Plugin.Log.LogWarning((object)"[FM26Export] Parado por falta de novos dados.");
				}
				if (_scrollAttempts >= _maxScrollAttempts)
				{
					Plugin.Log.LogWarning((object)("[FM26Export] Parado pelo limite de seguranca de scroll (" + _maxScrollAttempts + ")."));
				}
				_isComplete = true;
				return true;
			}
			_lastScrollY = num2;
			float num3;
			if (firstAncestorOfType != null)
			{
				Rect layout = ((VisualElement)firstAncestorOfType).layout;
				if (layout.height > 0f)
				{
					layout = ((VisualElement)firstAncestorOfType).layout;
					num3 = layout.height;
					goto IL_0369;
				}
			}
			num3 = 600f;
			goto IL_0369;
			IL_0369:
			// Scroll a third of the viewport at a time: a full-viewport jump overshoots the
			// virtualiser's currently-materialised content and clamps, which used to end the
			// capture prematurely. Smaller steps also give bindings time to fill row text.
			float num4 = Math.Max(60f, num3 / 3f);
			if (firstAncestorOfType != null)
			{
				firstAncestorOfType.scrollOffset = new Vector2(0f, num2 + num4);
			}
			_errorRetries = 0;
			_captureWait = PluginConfig.EffectiveScrollStepDelayFrames;
			return false;
		}
		catch (global::System.Exception ex)
		{
			// Tick can fire while UITK is mid-repaint (our pump hooks il2cpp_runtime_invoke);
			// mutating the tree then throws "cannot be marked for dirty repaint". Just retry next tick.
			if (ex.Message != null && ex.Message.Contains("dirty repaint"))
			{
				_captureWait = PluginConfig.EffectiveScrollStepDelayFrames;
				return false;
			}
			// Other exceptions on long captures are usually transient too (bindings still
			// filling, virtualiser mid-rebuild). Back off longer and retry a few times
			// instead of aborting the whole export.
			_errorRetries++;
			if (_errorRetries <= ERROR_RETRIES_MAX)
			{
				Plugin.Log.LogWarning((object)("[FM26Export] Erro transitório no CaptureStep (tentativa "
					+ _errorRetries + "/" + ERROR_RETRIES_MAX + "): " + ex.Message + " — aguardando e tentando de novo."));
				_captureWait = PluginConfig.EffectiveScrollStepDelayFrames * 4;
				return false;
			}
			ManualLogSource log4 = Plugin.Log;
			BepInExErrorLogInterpolatedStringHandler val5 = new BepInExErrorLogInterpolatedStringHandler(31, 1, out flag3);
			if (flag3)
			{
				((BepInExLogInterpolatedStringHandler)val5).AppendLiteral("[FM26Export] Erro CaptureStep: ");
				((BepInExLogInterpolatedStringHandler)val5).AppendFormatted<string>(ex.Message);
			}
			log4.LogError(val5);
			_isComplete = true;
			return true;
		}
	}

	public void FinishCapture()
	{
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Expected O, but got Unknown
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		bool flag = default(bool);
		try
		{
			if (_capturedRowCount == 0)
			{
				CloseOutput(deleteFiles: true);
				ManualLogSource log = Plugin.Log;
				BepInExWarningLogInterpolatedStringHandler val = new BepInExWarningLogInterpolatedStringHandler(31, 1, out flag);
				if (flag)
				{
					((BepInExLogInterpolatedStringHandler)val).AppendLiteral("[FM26Export] Nenhum dado para ");
					((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(FilePrefix);
					((BepInExLogInterpolatedStringHandler)val).AppendLiteral(".");
				}
				log.LogWarning(val);
				return;
			}
			CloseOutput(deleteFiles: false);
			ManualLogSource log2 = Plugin.Log;
			BepInExInfoLogInterpolatedStringHandler val2 = new BepInExInfoLogInterpolatedStringHandler(28, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export] OK ");
				((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(_capturedRowCount);
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" exportados.");
			}
			log2.LogInfo(val2);
			ManualLogSource log3 = Plugin.Log;
			val2 = new BepInExInfoLogInterpolatedStringHandler(27, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export] CSV salvo em: ");
				((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<string>(_csvFile);
			}
			log3.LogInfo(val2);
			ManualLogSource log4 = Plugin.Log;
			val2 = new BepInExInfoLogInterpolatedStringHandler(28, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export] HTML salvo em: ");
				((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<string>(_htmlFile);
			}
			log4.LogInfo(val2);
		}
		catch (global::System.Exception ex)
		{
			ManualLogSource log5 = Plugin.Log;
			BepInExErrorLogInterpolatedStringHandler val3 = new BepInExErrorLogInterpolatedStringHandler(33, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val3).AppendLiteral("[FM26Export] Erro FinishCapture: ");
				((BepInExLogInterpolatedStringHandler)val3).AppendFormatted<string>(ex.Message);
			}
			log5.LogError(val3);
		}
	}

	public virtual void Cleanup()
	{
		CloseOutput(_outputStarted && !_outputFinalized);
		_captureView = null;
		if (_captureHeaders != null)
		{
			_captureHeaders.Clear();
		}
		if (_seenKeys != null)
		{
			_seenKeys.Clear();
		}
	}

	private bool WriteCapturedRow(List<string> row)
	{
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected O, but got Unknown
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (row == null || row.Count == 0)
			{
				return true;
			}
			bool flag = true;
			var enumerator = row.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					if (!string.IsNullOrEmpty(enumerator.Current))
					{
						flag = false;
						break;
					}
				}
			}
			finally
			{
				((global::System.IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
			}
			if (flag)
			{
				return true;
			}
			EnsureOutputStarted();
			((TextWriter)_csvWriter).WriteLine(string.Join(";", (global::System.Collections.Generic.IEnumerable<string>)row.ConvertAll<string>((Converter<string, string>)UIUtils.Esc)));
			((TextWriter)_htmlWriter).WriteLine("<tr>");
			enumerator = row.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					string current = enumerator.Current;
					((TextWriter)_htmlWriter).WriteLine("\t<td>" + HtmlEsc(current) + "</td>");
				}
			}
			finally
			{
				((global::System.IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
			}
			((TextWriter)_htmlWriter).WriteLine("</tr>");
			_capturedRowCount++;
			return true;
		}
		catch (global::System.Exception ex)
		{
			ManualLogSource log = Plugin.Log;
			bool flag2 = default(bool);
			BepInExErrorLogInterpolatedStringHandler val = new BepInExErrorLogInterpolatedStringHandler(45, 1, out flag2);
			if (flag2)
			{
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral("[FM26Export] Erro ao gravar linha exportada: ");
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(ex.Message);
			}
			log.LogError(val);
			CloseOutput(deleteFiles: true);
			return false;
		}
	}

	private void EnsureOutputStarted()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Expected O, but got Unknown
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Expected O, but got Unknown
		if (_outputStarted)
		{
			return;
		}
		string text = global::System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
		string text2 = Path.Combine(Environment.GetFolderPath((Environment.SpecialFolder)5), "Sports Interactive", "Football Manager 26", "FM26PlayerExport by vinteset");
		string text3 = Path.Combine(text2, "Exports CSV");
		string text4 = Path.Combine(text2, "Exports HTML");
		Directory.CreateDirectory(text3);
		Directory.CreateDirectory(text4);
		_csvFile = Path.Combine(text3, FilePrefix + text + ".csv");
		_htmlFile = Path.Combine(text4, FilePrefix + text + ".html");
		_csvWriter = new StreamWriter(_csvFile, false, (Encoding)new UTF8Encoding(false), 65536);
		_htmlWriter = new StreamWriter(_htmlFile, false, (Encoding)new UTF8Encoding(false), 65536);
		((TextWriter)_csvWriter).WriteLine(string.Join(";", (global::System.Collections.Generic.IEnumerable<string>)_captureHeaders));
		((TextWriter)_htmlWriter).WriteLine("<html>");
		((TextWriter)_htmlWriter).WriteLine("<head>");
		((TextWriter)_htmlWriter).WriteLine("<meta charset=\"UTF-8\">");
		((TextWriter)_htmlWriter).WriteLine("<style type =\"text/css\">");
		((TextWriter)_htmlWriter).WriteLine("body,td,th { font-family: Verdana, Arial, Helvetica, sans-serif; font-size: 12px; }");
		((TextWriter)_htmlWriter).WriteLine("th { padding: 5px; text-align: left; background-color: #EEEEEE; border: 1px solid #000000; font-weight: bold; }");
		((TextWriter)_htmlWriter).WriteLine("td { padding: 4px; border: 1px solid #000000; }");
		((TextWriter)_htmlWriter).WriteLine("table { border-collapse: collapse; width: 98%; margin: 20px auto; }");
		((TextWriter)_htmlWriter).WriteLine("tr:nth-child(even) { background-color: #F9F9F9; }");
		((TextWriter)_htmlWriter).WriteLine("</style>");
		((TextWriter)_htmlWriter).WriteLine("</head>");
		((TextWriter)_htmlWriter).WriteLine("<body>");
		((TextWriter)_htmlWriter).WriteLine("<table border=\"1\">");
		((TextWriter)_htmlWriter).WriteLine("<tr>");
		var enumerator = _captureHeaders.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				string current = enumerator.Current;
				((TextWriter)_htmlWriter).WriteLine("\t<th>" + HtmlEsc(current) + "</th>");
			}
		}
		finally
		{
			((global::System.IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
		}
		((TextWriter)_htmlWriter).WriteLine("</tr>");
		_outputStarted = true;
		ManualLogSource log = Plugin.Log;
		bool flag = default(bool);
		BepInExInfoLogInterpolatedStringHandler val = new BepInExInfoLogInterpolatedStringHandler(55, 2, out flag);
		if (flag)
		{
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral("[FM26Export] Escrita incremental iniciada: CSV=");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(_csvFile);
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral(" | HTML=");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(_htmlFile);
		}
		log.LogInfo(val);
	}

	private void CloseOutput(bool deleteFiles)
	{
		try
		{
			if (_outputStarted && !_outputFinalized && _htmlWriter != null)
			{
				((TextWriter)_htmlWriter).WriteLine("</table></body></html>");
			}
		}
		catch
		{
		}
		try
		{
			StreamWriter csvWriter = _csvWriter;
			if (csvWriter != null)
			{
				((TextWriter)csvWriter).Dispose();
			}
		}
		catch
		{
		}
		try
		{
			StreamWriter htmlWriter = _htmlWriter;
			if (htmlWriter != null)
			{
				((TextWriter)htmlWriter).Dispose();
			}
		}
		catch
		{
		}
		_csvWriter = null;
		_htmlWriter = null;
		if (deleteFiles)
		{
			TryDelete(_csvFile);
			TryDelete(_htmlFile);
		}
		if (_outputStarted && !deleteFiles)
		{
			_outputFinalized = true;
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (!string.IsNullOrEmpty(path) && File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static string HtmlEsc(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
			.Replace("\"", "&quot;")
			.Replace("'", "&#39;");
	}

	private List<string> ReadRow(VisualElement row, bool diag, List<string> headers)
	{
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		List<string> val = new List<string>();
		if (row == null || row.childCount == 0)
		{
			return val;
		}
		VisualElement val2 = row.ElementAt(0);
		if (val2.childCount == 1 && val2.ElementAt(0).childCount > 1)
		{
			val2 = val2.ElementAt(0);
		}
		if (diag)
		{
			Plugin.Log.LogInfo((object)("[FM26Export] ROW DIAG: row.childCount=" + row.childCount + " cellContainer.childCount=" + val2.childCount + " containerType=" + UIUtils.RealTypeName(val2)));
			int diagMax = Math.Min(val2.childCount, 8);
			for (int di = 0; di < diagMax; di++)
			{
				Plugin.Log.LogInfo((object)("[FM26Export] Celula[" + di + "] DIAG: " + UIUtils.DiagCell(val2.ElementAt(di))));
			}
			if (val2.childCount > 3)
			{
				Plugin.Log.LogInfo((object)("[FM26Export] PROBE cell[3] leaf: " + UIUtils.ProbeText(UIUtils.FindLeaf(val2.ElementAt(3)))));
			}
		}
		for (int i = 1; i < val2.childCount; i++)
		{
			VisualElement val3 = val2.ElementAt(i);
			string text;
			if (i == 1)
			{
				List<string> val4 = new List<string>();
				UIUtils.CollectAllTexts(val3, val4);
				text = string.Empty;
				var enumerator = val4.GetEnumerator();
				try
				{
					while (enumerator.MoveNext())
					{
						string current = enumerator.Current;
						if (current.Length > text.Length)
						{
							text = current;
						}
					}
				}
				finally
				{
					((global::System.IDisposable)enumerator/*cast due to constrained. prefix*/).Dispose();
				}
			}
			else
			{
				text = UIUtils.CollectFirstText(val3) ?? string.Empty;
				if (string.IsNullOrEmpty(text))
				{
					string text2 = UIUtils.TryReadStars(val3);
					if (text2 != null)
					{
						text = text2;
					}
				}
			}
			val.Add(text);
		}
		return val;
	}

	private void FindAllByName(VisualElement root, string name, List<VisualElement> results)
	{
		if (root != null)
		{
			if (root.name == name)
			{
				results.Add(root);
			}
			for (int i = 0; i < root.childCount; i++)
			{
				FindAllByName(root.ElementAt(i), name, results);
			}
		}
	}
}
