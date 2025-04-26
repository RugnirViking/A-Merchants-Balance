using Godot;
using System;
using Godot.Collections;

public class Quest
{
	public string _questName;
	public  string _questText;
	public  string _questMission;
	public  string _questReward;
	
	public string _cityTarget;
	public int _goodType;
	public int _quanityNeeded;
	private int _quantityDelivered;
	public int _questRewardGold;
	
	public bool DeliveredGoodToCity(int goodType, int quantity, string cityDelivered, QuestContainer questContainer)
	{
		if (cityDelivered == _cityTarget){
			if (goodType == _goodType){
				if (quantity +  _quantityDelivered >= _quanityNeeded)
				{
					// complete the quest
					GameState.activeQuests.Remove(this);
					GameState.playerGold+=_questRewardGold;
					return true;
				}
				else
				{
					// we made some progress towards the goal
					_quantityDelivered += quantity;
					questContainer.QuestMission = $"SELL {_quantityDelivered}/{_quanityNeeded} {GameState.goodsNames[goodType].ToUpper()} IN {_cityTarget}";
					return false;
				}
			}
		}
		return false;
	}
	
	
	public Dictionary ToDictionary()
	{
		return new Dictionary {
			["questName"]        = _questName,
			["questText"]        = _questText,
			["questMission"]     = _questMission,
			["questReward"]      = _questReward,
			["cityTarget"]       = _cityTarget,
			["goodType"]         = _goodType,
			["quantityNeeded"]   = _quanityNeeded,
			["quantityDelivered"]= _quantityDelivered,
			["questRewardGold"]  = _questRewardGold
		};
	}
	
	
	public static Quest FromDictionary(Dictionary d)
	{
		var q = new Quest
		{
			_questName        = (string)d["questName"],
			_questText        = (string)d["questText"],
			_questMission     = (string)d["questMission"],
			_questReward      = (string)d["questReward"],
			_cityTarget       = (string)d["cityTarget"],
			_goodType         = Convert.ToInt32(d["goodType"]),
			_quanityNeeded    = Convert.ToInt32(d["quantityNeeded"]),
			_questRewardGold  = Convert.ToInt32(d["questRewardGold"]),
			_quantityDelivered = Convert.ToInt32(d["quantityDelivered"])
		};
		return q;
	}
}

public class QuestContainer : HBoxContainer
{
	private string _questName;
	public string QuestName {
		get => _questName;
		set
		{
			if (_questName != value)
			{
				_questName = value;
				UpdateQuestNameLabel();
			}
		}
	}
	
	private string _questText;
	public string QuestText {
		get => _questText;
		set
		{
			if (_questText != value)
			{
				_questText = value;
				UpdateQuestTextLabel();
			}
		}
	}
	private string _questMission;
	public string QuestMission {
		get => _questMission;
		set
		{
			if (_questMission != value)
			{
				_questMission = value;
				UpdateQuestMissionLabel();
			}
		}
	}
	private string _questReward;
	public string QuestReward {
		get => _questReward;
		set
		{
			if (_questReward != value)
			{
				_questReward = value;
				UpdateQuestRewardLabel();
			}
		}
	}
	public bool showAcceptButton=true;
	public Quest quest;
	private Label               _questNameLabel;
	private Label               _questTextLabel;
	private Label               _questMissionLabel;
	private Label               _questRewardLabel;
	public CityView				CityView;
	
	public override void _Ready()
	{
		_questNameLabel    = FindNode("QuestTitleLabel",true,false) as Label;
		_questTextLabel    = FindNode("QuestTextLabel",true,false) as Label;
		_questMissionLabel = FindNode("QuestMissionLabel",true,false) as Label;
		_questRewardLabel  = FindNode("RewardLabel",true,false) as Label;
		
		if (!showAcceptButton)
		{
			TextureButton acceptBtn  = FindNode("AcceptBtn",true,false) as TextureButton;
			acceptBtn.Hide();
		}
	}
	private void UpdateQuestNameLabel()
	{
		_questNameLabel    = FindNode("QuestTitleLabel",true,false) as Label;
		_questNameLabel.Text = _questName;
	}
	private void UpdateQuestTextLabel()
	{
		_questTextLabel    = FindNode("QuestTextLabel",true,false) as Label;
		_questTextLabel.Text = _questText;
	}
	private void UpdateQuestMissionLabel()
	{
		_questMissionLabel = FindNode("QuestMissionLabel",true,false) as Label;
		_questMissionLabel.Text = _questMission;
	}
	private void UpdateQuestRewardLabel()
	{
		_questRewardLabel  = FindNode("RewardLabel",true,false) as Label;
		_questRewardLabel.Text = _questReward;
	}
	
	private void _on_AcceptBtn_pressed()
	{
		GameState.activeQuests.Add(quest);
		CityView.RepopulateTrackedQuests();
		Hide();
	}

}

