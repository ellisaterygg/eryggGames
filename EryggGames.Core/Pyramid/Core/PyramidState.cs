#nullable enable
using System.Collections.Generic;
using System.Linq;
using EryggGames.Core;

namespace EryggGames.Pyramid.Core;

public class PyramidState
{
	public List<CardModel> InitialDeal { get; set; } = new List<CardModel>();
	public CardModel?[][] Pyramid { get; set; } = new CardModel?[7][];
	public List<CardModel> Stock { get; set; } = new List<CardModel>();
	public List<CardModel> Waste { get; set; } = new List<CardModel>();
	public int DeckPasses { get; set; } = 0;
	public bool IsFinished { get; set; }
	public bool WinnableOnly { get; set; } = true;
	public string? BackgroundFile { get; set; }

	public PyramidState()
	{
		for (int i = 0; i < 7; i++) Pyramid[i] = new CardModel?[i + 1];
	}

	public PyramidState Clone()
	{
		var clone = new PyramidState();
		clone.InitialDeal = InitialDeal.ToList();
		for (int i = 0; i < 7; i++)
		{
			for (int j = 0; j <= i; j++)
				clone.Pyramid[i][j] = Pyramid[i][j];
		}
		clone.Stock = Stock.ToList();
		clone.Waste = Waste.ToList();
		clone.DeckPasses = DeckPasses;
		clone.IsFinished = IsFinished;
		clone.WinnableOnly = WinnableOnly;
		clone.BackgroundFile = BackgroundFile;
		return clone;
	}
}
