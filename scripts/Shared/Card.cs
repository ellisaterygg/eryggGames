using Godot;
using EryggGames.Core;

namespace EryggGames.Shared;

public partial class Card : Node2D
{
    public const float CardWidth  = 80f;
    public const float CardHeight = 112f;

    public Suit Suit { get; private set; }
    public Rank Rank { get; private set; }
    public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;
    public CardPile CurrentPile { get; set; }
    
    private bool _isFaceUp = true;
    public bool IsFaceUp 
    { 
        get => _isFaceUp; 
        set { _isFaceUp = value; QueueRedraw(); } 
    }

	private string _rankText;
	private Color  _suitColor;

	public void Init(Suit suit, Rank rank)
	{
		Suit = suit;
		Rank = rank;

		_suitColor = IsRed ? new Color(0.82f, 0f, 0f) : Colors.Black;

		_rankText = rank switch
		{
			Rank.Ace   => "A",
			Rank.Jack  => "J",
			Rank.Queen => "Q",
			Rank.King  => "K",
			_          => ((int)rank).ToString()
		};

		QueueRedraw();
	}

	public Rect2 GetGlobalRect() => new Rect2(GlobalPosition - new Vector2(CardWidth / 2, CardHeight / 2), CardWidth, CardHeight);

	public override void _Draw()
	{
		var rect = new Rect2(-CardWidth / 2, -CardHeight / 2, CardWidth, CardHeight);
		
		if (!IsFaceUp)
		{
			// Card Back
			DrawRect(rect, new Color(0.15f, 0.25f, 0.45f)); // Dark blue back
			DrawRect(rect, Colors.White, filled: false, width: 1.5f);
			
			// Simple design on the back
			var inner = rect.Grow(-8f);
			DrawRect(inner, new Color(1, 1, 1, 0.2f), filled: false, width: 1f);
			DrawLine(new Vector2(inner.Position.X, inner.Position.Y), new Vector2(inner.End.X, inner.End.Y), new Color(1, 1, 1, 0.1f));
			DrawLine(new Vector2(inner.End.X, inner.Position.Y), new Vector2(inner.Position.X, inner.End.Y), new Color(1, 1, 1, 0.1f));
			return;
		}

		DrawRect(rect, Colors.White);
		DrawRect(rect, new Color(0.25f, 0.25f, 0.25f), filled: false, width: 1.5f);

		if (_rankText == null) return;

		// Rank (letters/numbers work fine with DrawString)
		DrawString(ThemeDB.FallbackFont,
				   new Vector2(-CardWidth / 2 + 4, -CardHeight / 2 + 24),
				   _rankText, HorizontalAlignment.Left, -1, 24, _suitColor);

		// Small suit – top right, same visual size as font
		DrawSuit(new Vector2(CardWidth / 2 - 15, -CardHeight / 2 + 14), 11f);

		// Large suit – centre
		DrawSuit(new Vector2(0f, 6f), 21f);
	}

	// ── Suit drawing ───────────────────────────────────────────────────────

	private void DrawSuit(Vector2 c, float s)
	{
		switch (Suit)
		{
			case Suit.Diamonds: DrawDiamond(c, s); break;
			case Suit.Hearts:   DrawHeart(c, s);   break;
			case Suit.Clubs:    DrawClub(c, s);    break;
			case Suit.Spades:   DrawSpade(c, s);   break;
		}
	}

	private void DrawDiamond(Vector2 c, float s)
	{
		DrawPolygon(new[]
		{
			c + new Vector2(0,        -s),
			c + new Vector2(s * 0.65f, 0),
			c + new Vector2(0,         s),
			c + new Vector2(-s * 0.65f, 0),
		}, new[] { _suitColor, _suitColor, _suitColor, _suitColor });
	}

	private void DrawHeart(Vector2 c, float s)
	{
		float r = s * 0.52f;
		DrawCircle(c + new Vector2(-r * 0.68f, -s * 0.18f), r, _suitColor);
		DrawCircle(c + new Vector2( r * 0.68f, -s * 0.18f), r, _suitColor);
		DrawPolygon(new[]
		{
			c + new Vector2(-s,      -s * 0.08f),
			c + new Vector2( s,      -s * 0.08f),
			c + new Vector2( 0,       s * 0.88f),
		}, new[] { _suitColor, _suitColor, _suitColor });
	}

	private void DrawClub(Vector2 c, float s)
	{
		float r = s * 0.38f;
		DrawCircle(c + new Vector2(0,          -s * 0.35f), r, _suitColor);
		DrawCircle(c + new Vector2(-s * 0.42f,  s * 0.18f), r, _suitColor);
		DrawCircle(c + new Vector2( s * 0.42f,  s * 0.18f), r, _suitColor);
		// Stem + base
		DrawRect(new Rect2(c.X - s * 0.13f, c.Y + s * 0.28f, s * 0.26f, s * 0.52f), _suitColor);
		DrawRect(new Rect2(c.X - s * 0.35f, c.Y + s * 0.72f, s * 0.70f, s * 0.18f), _suitColor);
	}

	private void DrawSpade(Vector2 c, float s)
	{
		// Upward triangle (blade)
		DrawPolygon(new[]
		{
			c + new Vector2(-s,       s * 0.22f),
			c + new Vector2( s,       s * 0.22f),
			c + new Vector2( 0,      -s * 0.88f),
		}, new[] { _suitColor, _suitColor, _suitColor });
		// Two shoulder circles
		float r = s * 0.50f;
		DrawCircle(c + new Vector2(-r * 0.68f, s * 0.12f), r, _suitColor);
		DrawCircle(c + new Vector2( r * 0.68f, s * 0.12f), r, _suitColor);
		// Stem + base
		DrawRect(new Rect2(c.X - s * 0.13f, c.Y + s * 0.28f, s * 0.26f, s * 0.52f), _suitColor);
		DrawRect(new Rect2(c.X - s * 0.35f, c.Y + s * 0.72f, s * 0.70f, s * 0.18f), _suitColor);
	}
}
