using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FM26PlayerExport.Handlers;

public class StaffExportHandler : GenericScrolledTableHandler
{
	public override bool TryStartCapture(VisualElement root, out string errorMessage)
	{
		if (base.TryStartCapture(root, out errorMessage))
		{
			base.FilePrefix = "staff_export_";
			return true;
		}
		return false;
	}

	protected override bool IsValidScreen(VisualElement root, List<string> headers)
	{
		if (_captureView == null)
		{
			return false;
		}
		for (VisualElement val = _captureView; val != null; val = val.parent)
		{
			if (!string.IsNullOrEmpty(val.name))
			{
				string text = val.name.ToLower();
				if (text.Contains("staff") || text.Contains("non_player"))
				{
					return true;
				}
			}
		}
		return false;
	}
}
