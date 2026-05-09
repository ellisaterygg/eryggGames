using System;
using System.Collections.Generic;
using EryggGames.FreeCell.Core;

namespace EryggGames.FreeCell;

public class Deck
{
    private readonly List<(Suit suit, Rank rank)> _cards = new();
    private readonly Random _rng;

    public Deck(int? seed = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        foreach (Suit suit in Enum.GetValues<Suit>())
            foreach (Rank rank in Enum.GetValues<Rank>())
                _cards.Add((suit, rank));
    }

    public List<(Suit suit, Rank rank)> Shuffle()
    {
        var deck = new List<(Suit, Rank)>(_cards);
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }
}
