using Godot;
using System;

public class mainMenu : Control
{
	
	private HBoxContainer    _tutorialContainer;
	private Popup            _tutorialPopup;
	private SaveLoadDialog   _saveLoadDialog;
	private string    		 _lastSaveKey;
	
	public override void _Ready()
	{
		_tutorialContainer = FindNode("TutorialPopup",true,false) as HBoxContainer;
		_tutorialPopup = FindNode("Tutorial",true,false) as Popup;
		_saveLoadDialog = FindNode("SaveLoadDialog", true, false) as SaveLoadDialog;
		_saveLoadDialog.Connect("popup_hide",    this, nameof(OnSaveLoadClosed));
		
		ShowHideButtons();
		
	}
	
	private void ShowHideButtons()
	{
		if (SaveManager.GetSaveKeys().Count>0){
			_lastSaveKey = SaveManager.Load("lastSaved");
		} 
		else 
		{
			// can't continue and can't load existing games - no saved games!
			Button continueButton = FindNode("Continue", true, false) as Button;
			Button loadGameButton = FindNode("LoadGame", true, false) as Button;
			continueButton.Hide();
			loadGameButton.Hide();
		}
	}
	
	private void OnSaveLoadClosed()
	{
		if (!GameState.isFirstLoad)
		{
			// probably loaded a save
			// go to Node2D
			var world = (PackedScene)GD.Load("res://Node2D.tscn");
			GetTree().ChangeSceneTo(world);
		}
		// otherwise they probably just quit out
	}

	private void _on_New_game_pressed()
	{
		_tutorialPopup.PopupCentered();
	}
	
	private void _on_Load_game_pressed()
	{
		_saveLoadDialog.PopupCentered();
	}
	
	private void _on_Continue_pressed()
	{
		GameState.CurrentSaveGameKey = _lastSaveKey;
		GameState.LoadGame();
		var world = (PackedScene)GD.Load("res://Node2D.tscn");
		GetTree().ChangeSceneTo(world);
	}
	
	private void _on_No_include_tutorial_pressed()
	{
		var world = (PackedScene)GD.Load("res://Node2D.tscn");
		GetTree().ChangeSceneTo(world);
	}


	private void _on_Yes_tutorial_pressed()
	{
		// Replace with function body.
	}


	private void _on_Yes_dont_ask_again_pressed()
	{
		// Replace with function body.
	}
}
