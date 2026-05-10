using Godot;
using System.Collections.Generic;
using EryggGames.Core;

namespace EryggGames.Shared;

public partial class CardPile : Node2D
{
    public PileType PileType { get; set; }
    public Suit? FoundationSuit { get; set; }

    public readonly List<Card> Cards = new();
    public float CardOffset { get; set; } = 55f;

    public Card TopCard => Cards.Count > 0 ? Cards[^1] : null;
    public int Count => Cards.Count;
    public bool IsEmpty => Cards.Count == 0;

    public Rect2 GetGlobalRect() => new Rect2(GlobalPosition - new Vector2(Card.CardWidth / 2, Card.CardHeight / 2), Card.CardWidth, Card.CardHeight);

    public void AddCard(Card card)
    {
        bool wasEmpty = IsEmpty;
        Cards.Add(card);
        card.CurrentPile = this;
        if (card.GetParent() != null && card.GetParent() != this)
            card.Reparent(this);
        else if (card.GetParent() == null)
            AddChild(card);
        card.Position = GetCardPosition(Cards.Count - 1);
        card.ZIndex = Cards.Count;
        if (wasEmpty) QueueRedraw();
    }

    public Card RemoveTopCard()
    {
        if (IsEmpty) return null;
        var card = Cards[^1];
        Cards.RemoveAt(Cards.Count - 1);
        RemoveChild(card);
        if (IsEmpty) QueueRedraw();
        return card;
    }

    // Removes top N cards, returns them bottom-first (ready to re-add in order)
    public List<Card> RemoveTopCards(int count)
    {
        var removed = new List<Card>();
        for (int i = 0; i < count; i++)
            removed.Insert(0, RemoveTopCard());
        return removed;
    }

    public override void _Draw()
    {
        if (!IsEmpty) return;
        var w = Card.CardWidth;
        var h = Card.CardHeight;
        var (fill, border) = PileType switch
        {
            PileType.FreeCell   => (new Color(0.4f, 0.6f, 1.0f, 0.15f), new Color(0.5f, 0.7f, 1.0f, 0.55f)),
            PileType.Foundation => (new Color(0.9f, 0.8f, 0.2f, 0.15f), new Color(1.0f, 0.85f, 0.3f, 0.55f)),
            _                   => (new Color(1.0f, 1.0f, 1.0f, 0.15f), new Color(1.0f, 1.0f, 1.0f, 0.45f)),
        };
        DrawRect(new Rect2(-w / 2, -h / 2, w, h), fill);
        DrawRect(new Rect2(-w / 2, -h / 2, w, h), border, filled: false, width: 2f);
    }

    private Vector2 GetCardPosition(int index) =>
        PileType == PileType.Tableau
            ? new Vector2(0, index * CardOffset)
            : Vector2.Zero;
}
