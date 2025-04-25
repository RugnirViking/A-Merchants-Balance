using Godot;
using System;
using System.Collections.Generic;

public class GraphControl : Control
{
	[Export] public NodePath LinePath = "Line";
	[Export] public float YScale = 5f;     // how many pixels per unit of your data
	[Export] public float XSpacing = 2f;   // horizontal spacing between points

	private Line2D _line;
	private List<float> _values = new List<float>();

	public override void _Ready()
	{
		_line = GetNode<Line2D>(LinePath);
	}

	/// <summary>
	/// Pushes a new sample onto the graph, dropping old points when full.
	/// </summary>
	public void AddValue(float value)
	{
		_values.Add(value);

		// cap point count so it never exceeds the width in pixels / spacing
		int maxPoints = Mathf.CeilToInt(RectSize.x / XSpacing);
		if (_values.Count > maxPoints)
			_values.RemoveAt(0);

		UpdateLine();
	}

	private void UpdateLine()
	{
		var pts = new Vector2[_values.Count];
		for (int i = 0; i < _values.Count; i++)
		{
			float x = i * XSpacing;
			// invert Y so larger values go upward
			float y = RectSize.y - (_values[i] * YScale);
			pts[i] = new Vector2(x, y);
		}
		_line.Points = pts;
	}
}
