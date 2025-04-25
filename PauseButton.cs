using Godot;
using System;

public class PauseButton : Button
{
	public override void _Ready()
	{
		
	}
	
	
	private void _on_Button_pressed()
	{
		GD.Print("Clicked too");
		
		GetViewport().SetInputAsHandled();
	}

}


