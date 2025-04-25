using Godot;
using System;
using System.Collections.Generic;


public class GraphControl : Control
{
	[Export] public NodePath[] LinePaths;          // Paths to multiple Line2D nodes
	[Export] public float YScale = 5f;             // pixels per unit (vertical)
	[Export] public float XSpacing = 2f;           // pixels between points (horizontal)
	[Export] public Color AxisColor = new Color(0f, 0f, 0f);
	[Export] public float AxisWidth = 2f;

	[Export] public Color TickColor = new Color(0f, 0f, 0f);
	[Export] public Color LabelColor = new Color(0f, 0f, 0f);
	[Export] public int XTickSpacing = 50;          // pixels between vertical tick marks
	[Export] public int YTickSpacing = 50;          // pixels between horizontal tick marks
	[Export] public int LabelFontSize = 12;

	private List<Line2D> _lines = new List<Line2D>();
	private List<List<float>> _seriesValues = new List<List<float>>();
	private Font _font;

	public override void _Ready()
	{
		// Fetch all Line2D nodes
		foreach (var path in LinePaths)
		{
			var line = GetNode<Line2D>(path);
			line.Position = Vector2.Zero;
			_lines.Add(line);
			_seriesValues.Add(new List<float>());
		}

		// Load font
		var dynamicFont = new DynamicFont();
		dynamicFont.FontData = GD.Load<DynamicFontData>("res://Barlow-Black.ttf");
		dynamicFont.Size = LabelFontSize;
		_font = dynamicFont;
	}

	/// <summary>
	/// Add a new set of price values for each series (length must match LinePaths.Length).
	/// </summary>
	public void AddValues(float[] values)
	{
		if (values.Length != _seriesValues.Count)
			return;

		int maxPoints = Mathf.CeilToInt(RectSize.x / XSpacing);
		for (int i = 0; i < values.Length; i++)
		{
			var list = _seriesValues[i];
			list.Add(values[i]);
			if (list.Count > maxPoints)
				list.RemoveAt(0);
		}

		UpdateLines();
		Update(); // trigger _Draw
	}

	private void UpdateLines()
	{
		for (int s = 0; s < _lines.Count; s++)
		{
			var pts = new Vector2[_seriesValues[s].Count];
			for (int i = 0; i < pts.Length; i++)
			{
				float x = i * XSpacing;
				float y = RectSize.y - (_seriesValues[s][i] * YScale);
				pts[i] = new Vector2(x, y);
			}
			_lines[s].Points = pts;
		}
	}

	public override void _Draw()
	{
		// Draw axes
		DrawLine(new Vector2(0, RectSize.y), new Vector2(RectSize.x, RectSize.y), AxisColor, AxisWidth);
		DrawLine(new Vector2(0, 0), new Vector2(0, RectSize.y), AxisColor, AxisWidth);

		// X ticks and labels
		for (int x = 0; x < RectSize.x; x += XTickSpacing)
		{
			DrawLine(new Vector2(x, RectSize.y - 5), new Vector2(x, RectSize.y + 5), TickColor, 1f);
			string label = (x / XSpacing).ToString("0");
			DrawString(_font, new Vector2(x + 2, RectSize.y + 14), label, LabelColor);
		}

		// Y ticks and labels
		for (int y = 0; y < RectSize.y; y += YTickSpacing)
		{
			DrawLine(new Vector2(-5, RectSize.y - y), new Vector2(5, RectSize.y - y), TickColor, 1f);
			string label = (y / YScale).ToString("0");
			DrawString(_font, new Vector2(8, RectSize.y - y + 5), label, LabelColor);
		}
	}
}

