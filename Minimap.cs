using Godot;
using System;

public class Minimap : Control
{
	public BaseGameHandler _baseGameHandler;
	private bool _isDragging = false;
	
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton btn)
		{
			Vector2 mousePos = btn.Position;
			
			if (btn.Pressed)
			{
				_isDragging = true;
				if (GetGlobalRect().HasPoint(mousePos))
				{
					Vector2 worldTarget = _baseGameHandler.MinimapClickToWorld(mousePos);
					_baseGameHandler.SetWorldTarget(worldTarget);
					
				}
			}
			else
			{
				_isDragging = false;
			}
			
		}
		else if (@event is InputEventMouseMotion mm && _isDragging)
		{
			
			Vector2 mousePos = mm.Position;
			if (GetGlobalRect().HasPoint(mousePos))
			{
				Vector2 worldTarget = _baseGameHandler.MinimapClickToWorld(mousePos);
				_baseGameHandler.SetWorldTarget(worldTarget);
			}
		}
	}
}
