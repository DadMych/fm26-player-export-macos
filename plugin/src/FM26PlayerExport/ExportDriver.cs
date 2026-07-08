using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using FM26PlayerExport.Handlers;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FM26PlayerExport;

/// <summary>
/// Plain (non-MonoBehaviour) driver for the export. On arm64 macOS we cannot inject a
/// MonoBehaviour via Il2CppInterop.ClassInjector (its function finders are x86-only), so instead
/// of running on Unity's Update loop this class is ticked from BepInEx's main-thread pump
/// (IL2CPPChainloader.MainThreadTick, driven by the il2cpp_runtime_invoke detour). All work here
/// only READS existing il2cpp objects, which interop supports on arm64.
/// </summary>
public sealed class ExportDriver
{
	private IExportHandler _currentHandler;
	private readonly List<IExportHandler> _availableHandlers = new();
	private bool _started;

	private void EnsureStarted()
	{
		if (_started) return;
		_started = true;
		_availableHandlers.Add(new StaffExportHandler());
		_availableHandlers.Add(new PlayerExportHandler());
		Plugin.Log.LogInfo("[FM26Export] Driver pronto (main-thread pump). [F9 / Ctrl+P] = exportar.");
	}

	public void Tick()
	{
		try
		{
			EnsureStarted();
			if (Keyboard.current == null)
				return;

			if (_currentHandler == null)
			{
				var ctrl = ((ButtonControl)Keyboard.current.leftCtrlKey).isPressed
						   || ((ButtonControl)Keyboard.current.rightCtrlKey).isPressed;
				var ctrlP = ctrl && ((ButtonControl)Keyboard.current.pKey).wasPressedThisFrame;
				var f9 = ((ButtonControl)Keyboard.current.f9Key).wasPressedThisFrame;
				if (ctrlP || f9)
				{
					Plugin.Log.LogInfo("[FM26Export] Iniciando exportação via atalho: " + (f9 ? "F9" : "Ctrl+P"));
					StartCapture();
				}
			}
			else if (_currentHandler.CaptureStep())
			{
				_currentHandler.FinishCapture();
				try { _currentHandler.Cleanup(); } catch { }
				_currentHandler = null;
				try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
			}
		}
		catch (Exception)
		{
		}
	}

	private VisualElement GetMainRoot()
	{
		try
		{
			Il2CppArrayBase<UIDocument> docs = Object.FindObjectsOfType<UIDocument>();
			if (docs == null) return null;
			var e = docs.GetEnumerator();
			try
			{
				while (((global::System.Collections.IEnumerator)e).MoveNext())
				{
					UIDocument cur = e.Current;
					if ((Object)(object)cur != (Object)null
						&& cur.rootVisualElement != null
						&& cur.rootVisualElement.name == "PanelManager-container")
						return cur.rootVisualElement;
				}
			}
			finally { ((global::System.IDisposable)e)?.Dispose(); }
		}
		catch { }
		return null;
	}

	private void StartCapture()
	{
		try
		{
			VisualElement root = GetMainRoot();
			if (root == null)
			{
				Plugin.Log.LogError("[FM26Export] Sem UIDocument (Painel principal não encontrado).");
				return;
			}
			foreach (var h in _availableHandlers)
			{
				if (h.TryStartCapture(root, out var err))
				{
					Plugin.Log.LogInfo("[FM26Export] Usando handler: " + ((MemberInfo)((object)h).GetType()).Name);
					_currentHandler = h;
					return;
				}
				if (!string.IsNullOrEmpty(err))
					Plugin.Log.LogInfo(err);
			}
			Plugin.Log.LogWarning("[FM26Export] Nenhuma tela suportada para exportação encontrada no momento.");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("[FM26Export] Erro ao iniciar captura: " + ex.Message);
		}
	}
}
