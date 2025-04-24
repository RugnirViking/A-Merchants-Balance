using Godot;
using System;

public class CityView : Control
{
	// Declare member variables here. Examples:
	// private int a = 2;
	// private string b = "text";
	
	private Label               _cityNameLabel;
	private TextureRect         _bannerTexture;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_cityNameLabel           = FindNode("CityName",true,false) as Label;
		_bannerTexture           = FindNode("TextureRect",true,false) as TextureRect;
		
		_cityNameLabel.Text    =   GameState.CurrentCity;
		_bannerTexture.Texture =   GameState.CityBanner;
		
	}

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
	public void _on_Button_pressed()
	{
		var world = (PackedScene)GD.Load("res://Node2D.tscn");
		GetTree().ChangeSceneTo(world);
	}
}



