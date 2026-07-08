using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FM26PlayerExport.Handlers;

public class PlayerExportHandler : GenericScrolledTableHandler
{
	public PlayerExportHandler()
	{
		base.FilePrefix = "person_";
	}

	public override bool TryStartCapture(VisualElement root, out string errorMessage)
	{
		bool num = base.TryStartCapture(root, out errorMessage);
		if (num)
		{
			base.FilePrefix = "moneyball_export_";
		}
		return num;
	}

	protected override bool IsValidScreen(VisualElement root, List<string> headers)
	{
		return true;
	}
}
