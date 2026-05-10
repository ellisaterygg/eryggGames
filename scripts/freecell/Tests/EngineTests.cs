using Godot;
using System;
using System.Collections.Generic;
using EryggGames.Core;
using EryggGames.FreeCell.Core;

namespace EryggGames.FreeCell.Tests;

// Simple test runner for Core logic
public static class EngineTests
{
	public static void RunTests()
	{
		try
		{
			TestMaxMovable();
			TestCanMoveTableau();
			TestCanMoveFoundation();
			TestIsValidSequence();
			GD.Print("✅ All FreeCellEngine tests passed!");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"❌ Test Failed: {ex.Message}");
		}
	}

	private static void TestMaxMovable()
	{
		var state = new FreeCellState();
		
		// Fill all tableau columns so none are empty
		for (int i = 0; i < 8; i++)
			state.Tableau[i].Add(new CardModel(Suit.Clubs, Rank.King));
		
		// Fill all free cells so none are empty
		for (int i = 0; i < 4; i++)
			state.FreeCells[i].Add(new CardModel(Suit.Diamonds, Rank.King));

		// Base: (0 free cells + 1) * 2^0 = 1
		Assert(FreeCellEngine.CalculateMaxMovable(state) == 1, "MaxMovable base (all full)");

		// 1 free cell empty: (1+1) * 2^0 = 2
		state.FreeCells[0].Clear();
		Assert(FreeCellEngine.CalculateMaxMovable(state) == 2, "MaxMovable 1 free cell empty");
		
		// 1 empty column: (1+1) * 2^1 = 4
		state.Tableau[0].Clear();
		Assert(FreeCellEngine.CalculateMaxMovable(state) == 4, "MaxMovable 1 cell + 1 col empty");
	}

	private static void TestCanMoveTableau()
	{
		var state = new FreeCellState();
		state.Tableau[0].Add(new CardModel(Suit.Spades, Rank.Ten));
		
		var moving = new List<CardModel> { new CardModel(Suit.Hearts, Rank.Nine) };
		Assert(FreeCellEngine.CanMove(state, moving, PileType.Tableau, 0).IsValid, "Valid Tableau move");

		var invalidRank = new List<CardModel> { new CardModel(Suit.Hearts, Rank.Eight) };
		Assert(!FreeCellEngine.CanMove(state, invalidRank, PileType.Tableau, 0).IsValid, "Invalid Tableau move (rank)");

		var invalidSuit = new List<CardModel> { new CardModel(Suit.Clubs, Rank.Nine) };
		Assert(!FreeCellEngine.CanMove(state, invalidSuit, PileType.Tableau, 0).IsValid, "Invalid Tableau move (color)");
	}

	private static void TestCanMoveFoundation()
	{
		var state = new FreeCellState();
		var ace = new List<CardModel> { new CardModel(Suit.Hearts, Rank.Ace) };
		Assert(FreeCellEngine.CanMove(state, ace, PileType.Foundation, 0).IsValid, "Ace to empty foundation");

		state.Foundations[0].Add(new CardModel(Suit.Hearts, Rank.Ace));
		state.FoundationSuits[0] = Suit.Hearts;
		var two = new List<CardModel> { new CardModel(Suit.Hearts, Rank.Two) };
		Assert(FreeCellEngine.CanMove(state, two, PileType.Foundation, 0).IsValid, "Two to foundation");
		
		var wrongSuit = new List<CardModel> { new CardModel(Suit.Spades, Rank.Two) };
		Assert(!FreeCellEngine.CanMove(state, wrongSuit, PileType.Foundation, 0).IsValid, "Wrong suit to foundation");
	}

	private static void TestIsValidSequence()
	{
		var valid = new List<CardModel> {
			new CardModel(Suit.Spades, Rank.Ten),
			new CardModel(Suit.Hearts, Rank.Nine),
			new CardModel(Suit.Clubs, Rank.Eight)
		};
		Assert(FreeCellEngine.IsValidSequence(valid), "Valid sequence");

		var sameColor = new List<CardModel> {
			new CardModel(Suit.Spades, Rank.Ten),
			new CardModel(Suit.Clubs, Rank.Nine)
		};
		Assert(!FreeCellEngine.IsValidSequence(sameColor), "Invalid sequence (color)");
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition) throw new Exception("Test Failed: " + message);
	}
}
