using ApexPost.Application.DTOs;

namespace ApexPost.Application.Extensions;

public static class PagingExtensions
{
    extension<T>(IReadOnlyCollection<T> items)
    {
        /// <summary>Cuts one page out of an in-memory list.</summary>
        public PagedResult<T> ToPage(PageRequest page) =>
            new([.. items.Skip(page.Skip).Take(page.Size)], page.Number, page.Size, items.Count);
    }
}
