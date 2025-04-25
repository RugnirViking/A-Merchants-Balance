using Godot;
using System;

public class mainMenu : Control
{
	
	private HBoxContainer    _tutorialContainer;
	private Popup            _tutorialPopup;
	
	public override void _Ready()
	{
		_tutorialContainer = FindNode("TutorialPopup",true,false) as HBoxContainer;
		_tutorialPopup = FindNode("Tutorial",true,false) as Popup;
		
		_tutorialContainer.Hide(); // this container blocks input through it, so if its shown when the game launches, you can't press the buttons

	}


	private void _on_New_game_pressed()
	{
		_tutorialContainer.Show(); 
		_tutorialPopup.PopupCentered();
	}
	
	private void _on_Tutorial_popup_hide()
	{
		_tutorialContainer.Hide(); 
		_tutorialPopup.PopupCentered();
	}
}




