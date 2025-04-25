using Godot;
using System;
using System.Collections.Generic;

public class GraphControl : Control
{
	[Export] public NodePath LinePath = "Line";
	[Export] public float YScale = 5f;      // pixels per unit (vertical)
	[Export] public float XSpacing = 2f;    // pixels between points (horizontal)
	[Export] public Color AxisColor = new Color(0.0f, 0.0f, 0.0f);
	[Export] public float AxisWidth = 2f;
	
	
	[Export] public Color TickColor = new Color(0.0f, 0.0f, 0.0f);
	[Export] public Color LabelColor = new Color(0.0f, 0.0f, 0.0f);
	[Export] public int XTickSpacing = 50; // pixels between vertical tick marks
	[Export] public int YTickSpacing = 50; // pixels between horizontal tick marks
	[Export] public int LabelFontSize = 12;

	private Line2D _line;
	private List<float> _values = new List<float>();
	private Font _font;

	public override void _Ready()
	{
		_line = GetNode<Line2D>(LinePath);
		_line.Position = Vector2.Zero; // Important: anchor it inside the Control
		
		// Load a built-in dynamic font
		var dynamicFont = new DynamicFont();
		dynamicFont.FontData = GD.Load("res://Barlow-Black.ttf") as DynamicFontData;
		dynamicFont.Size = LabelFontSize;
		_font = dynamicFont;
	}

	public void AddValue(float value)
	{
		_values.Add(value);

		int maxPoints = Mathf.CeilToInt(RectSize.x / XSpacing);
		if (_values.Count > maxPoints)
			_values.RemoveAt(0);

		UpdateLine();
		Update(); // request _Draw()
	}

	private void UpdateLine()
	{
		var pts = new Vector2[_values.Count];
		for (int i = 0; i < _values.Count; i++)
		{
			float x = i * XSpacing;
			float y = RectSize.y - (_values[i] * YScale); // flip Y so up = positive
			pts[i] = new Vector2(x, y);
		}
		_line.Points = pts;
	}

	public override void _Draw()
	{
		// === Draw axes ===
		DrawLine(new Vector2(0, RectSize.y), new Vector2(RectSize.x, RectSize.y), AxisColor, AxisWidth); // X-axis
		DrawLine(new Vector2(0, 0), new Vector2(0, RectSize.y), AxisColor, AxisWidth); // Y-axis

		// === Draw X ticks ===
		for (int x = 0; x < RectSize.x; x += XTickSpacing)
		{
			Vector2 top = new Vector2(x, RectSize.y - 5);
			Vector2 bottom = new Vector2(x, RectSize.y + 5);
			DrawLine(top, bottom, TickColor, 1f);

			// Draw label
			string label = (x / XSpacing).ToString("0");
			DrawString(_font, new Vector2(x + 2, RectSize.y + 14), label, LabelColor);
		}

		// === Draw Y ticks ===
		for (int y = 0; y < RectSize.y; y += YTickSpacing)
		{
			Vector2 left = new Vector2(-5, RectSize.y - y);
			Vector2 right = new Vector2(5, RectSize.y - y);
			DrawLine(left, right, TickColor, 1f);

			// Draw label
			string label = (y / YScale).ToString("0");
			DrawString(_font, new Vector2(8, RectSize.y - y + 5), label, LabelColor);
		}
	}
}
