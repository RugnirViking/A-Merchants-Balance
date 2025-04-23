using Godot;
using System;

public class ClickDebugVis : Node2D
{
	[Export] public int MarkerSize = 6;   // px

	private BaseGameHandler _handler;

	private ColorRect _rawMarker;
	private ColorRect _unwarpMarker;

	public override void _Ready()
	{
		// grab the handler on the same parent (tweak the path if needed)
		_handler = GetParent<BaseGameHandler>(); 

		// green square = raw mouse
		_rawMarker = MakeMarker(new Color(0, 1, 0));   // lime
		// red square = inverse‑mapped UV
		_unwarpMarker = MakeMarker(new Color(1, 0, 0)); // red

		// Add as children so they draw
		AddChild(_rawMarker);
		AddChild(_unwarpMarker);
	}

	private ColorRect MakeMarker(Color c)
	{
		ColorRect r = new ColorRect
		{
			Color     = c,
			RectSize  = new Vector2(MarkerSize, MarkerSize),
			RectPivotOffset = new Vector2(MarkerSize, MarkerSize) * 0.5f
		};
		return r;
	}

	public override void _Process(float delta)
	{
		Vector2 vpSize   = GetViewport().Size;
		Vector2 mousePx  = GetViewport().GetMousePosition();

		// green marker (raw)
		_rawMarker.RectGlobalPosition = mousePx;

		// red marker (inverse‑mapped)
		Vector2 mouseUV   = mousePx / vpSize;
		Vector2 unwarpUV  = _handler._targetPos;
		Vector2 unwarpPx  = new Vector2(unwarpUV.x * vpSize.x,
										unwarpUV.y * vpSize.y);

		_unwarpMarker.RectGlobalPosition = unwarpPx;
	}
}
