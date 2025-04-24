using Godot;
using System;

public class LabelPrototype : NinePatchRect
{
	public override void _Ready()
	{
		ResizeToFit();
	}

	public void SetText(string text, float maxWidth = 400)
	{
		var label = GetNode<Label>("LabelPrototype2/TextForLabel");
		label.Text = text;
		label.Autowrap = true;
		label.RectMinSize = new Vector2(maxWidth, 0);

		// Let layout catch up
		label.CallDeferred("minimum_size_changed");
		CallDeferred(nameof(ResizeToFit));
	}

	private void ResizeToFit()
	{
		var container = GetNode<PanelContainer>("LabelPrototype2");
		Vector2 baseSize = container.GetCombinedMinimumSize();

		Vector2 paddedSize = baseSize + new Vector2(40, 10);
		RectSize = paddedSize;
	}
}
