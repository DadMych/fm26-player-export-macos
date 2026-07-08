using UnityEngine.UIElements;

namespace FM26PlayerExport.Handlers;

public interface IExportHandler
{
	bool TryStartCapture(VisualElement root, out string errorMessage);

	bool CaptureStep();

	void FinishCapture();

	void Cleanup();
}
