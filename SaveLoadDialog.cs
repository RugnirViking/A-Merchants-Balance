// SaveLoadDialog.cs
using Godot;
using System;
using System.Collections.Generic;

public class SaveLoadDialog : WindowDialog
{
	[Export]
	public bool IsMainMenu = false;
	
	private ItemList _saveList;
	private Label    _detailsLabel;
	private Button   _loadBtn, _overwriteBtn, _newBtn, _closeBtn;

	// ← add this
	private int _selectedIndex = -1;

	public override void _Ready()
	{
		_saveList      = GetNode<ItemList>("MarginContainer/VBoxContainer/HBoxContainer/SaveList");
		_detailsLabel  = GetNode<Label>   ("MarginContainer/VBoxContainer/HBoxContainer/PanelContainer/VBoxContainer/DetailsLabel");
		_loadBtn       = GetNode<Button>  ("MarginContainer/VBoxContainer/Buttons/Load");
		_overwriteBtn  = GetNode<Button>  ("MarginContainer/VBoxContainer/Buttons/Overwrite");
		_newBtn        = GetNode<Button>  ("MarginContainer/VBoxContainer/Buttons/New");
		_closeBtn      = GetNode<Button>  ("MarginContainer/VBoxContainer/Buttons/Close");

		if (IsMainMenu)
		{
			_saveList.Connect("item_selected",    this, nameof(OnSaveSelected));
			_loadBtn.Connect   ("pressed",         this, nameof(OnLoadPressed));
			_closeBtn.Connect  ("pressed",         this, nameof(HideSelf));
			_newBtn.Disabled      = true;
		}
		else
		{
			_saveList.Connect("item_selected",    this, nameof(OnSaveSelected));
			_loadBtn.Connect   ("pressed",         this, nameof(OnLoadPressed));
			_overwriteBtn.Connect("pressed",       this, nameof(OnOverwritePressed));
			_newBtn.Connect    ("pressed",         this, nameof(OnNewPressed));
			_closeBtn.Connect  ("pressed",         this, nameof(HideSelf));
		}

		_loadBtn.Disabled      = true;
		_overwriteBtn.Disabled = true;
		PopulateSaveList();
	}

	private void PopulateSaveList()
	{
		_saveList.Clear();
		foreach (var key in SaveManager.GetSaveKeys())
			_saveList.AddItem(key);
		_selectedIndex = -1;   // reset
		_loadBtn.Disabled = true;
		_overwriteBtn.Disabled = IsMainMenu;
		_detailsLabel.Text = "";
	}

	// ← store the clicked index here
	private void OnSaveSelected(int index)
	{
		_selectedIndex = index;
		string key = _saveList.GetItemText(index);
		ShowDetails(key);
		_loadBtn.Disabled      = false;
		_overwriteBtn.Disabled = IsMainMenu;
	}

	private void ShowDetails(string key)
	{
		string json = SaveManager.Load(key);
		var parsed = JSON.Parse(json);
		if (parsed.Error != Error.Ok)
		{
			_detailsLabel.Text = "❌ Corrupt save.";
			return;
		}

		var d = parsed.Result as Godot.Collections.Dictionary;
		int gold = d.Contains("playerGold") ? Convert.ToInt32(d["playerGold"]) : 0;
		float x   = d.Contains("worldPosX")   ? Convert.ToSingle(d["worldPosX"]) : 0f;
		float y   = d.Contains("worldPosY")   ? Convert.ToSingle(d["worldPosY"]) : 0f;
		int cities = 0;
		if (d.Contains("CityEconomies"))
			cities = (d["CityEconomies"] as Godot.Collections.Dictionary).Count;

		_detailsLabel.Text =
			$"Gold: {gold}\n" +
			$"WorldPos: ({x:0.##},{y:0.##})\n" +
			$"Cities: {cities}";
	}
	
	private void OnLoadPressed()
	{
		if (_selectedIndex < 0)
			return;

		string key = _saveList.GetItemText(_selectedIndex);
		GameState.CurrentSaveGameKey = key;
		GameState.LoadGame();
		Hide();
	}

	private void OnOverwritePressed()
	{
		if (_selectedIndex < 0)
			return;

		string key = _saveList.GetItemText(_selectedIndex);
		GameState.CurrentSaveGameKey = key;
		GameState.SaveGame();
		PopulateSaveList();
	}
	private void OnNewPressed()
	{
		// ask for a new slot name
		var prompt = new AcceptDialog();
		prompt.DialogText = "Enter new slot name:";
		var le = new LineEdit { PlaceholderText = "slot name" };
		prompt.AddChild(le);
		prompt.AddButton("Create", true);
		prompt.Connect("confirmed", this, nameof(OnNewSlotConfirmed), new Godot.Collections.Array { le });
		prompt.RectMinSize = new Vector2(300, 150);  // width = 600px, height = 400px
		AddChild(prompt);
		prompt.PopupCentered();
	}

	private void OnNewSlotConfirmed(LineEdit le)
	{
		var name = le.Text.Trim();
		if (string.IsNullOrEmpty(name))
			return;

		GameState.CurrentSaveGameKey = name;
		GameState.SaveGame();
		PopulateSaveList();
	}
	
	private void HideSelf()
	{
		Hide();
	}
}


