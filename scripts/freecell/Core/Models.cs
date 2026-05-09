namespace EryggGames.FreeCell.Core;

public enum Suit { Clubs, Diamonds, Hearts, Spades }

public enum Rank { Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

public enum PileType { Tableau, FreeCell, Foundation }

public record CardModel(Suit Suit, Rank Rank)
{
    public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;
}
