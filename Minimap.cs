using Godot;
using System;

public class Minimap : Control
{
	public BaseGameHandler _baseGameHandler;
	
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton btn && btn.Pressed)
		{
			Vector2 mousePos = btn.Position;

			if (GetGlobalRect().HasPoint(mousePos))
			{
				Vector2 worldTarget = _baseGameHandler.MinimapClickToWorld(mousePos);
				_baseGameHandler.SetWorldTarget(worldTarget);
				
			}
		}
	}
}
