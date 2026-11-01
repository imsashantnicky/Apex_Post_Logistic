namespace ApexPost.Application.DTOs;

/// <summary>A page of a list: page numbers start at 1 and the size is kept between 1 and 100.</summary>
public sealed record PageRequest
{
    public const int DefaultSize = 20;
    public const int MaximumSize = 100;

    public PageRequest(int number = 1, int size = DefaultSize)
    {
        Number = Math.Max(1, number);
        Size = Math.Clamp(size, 1, MaximumSize);
    }

    public int Number { get; }

    public int Size { get; }

    public int Skip => (Number - 1) * Size;
}
