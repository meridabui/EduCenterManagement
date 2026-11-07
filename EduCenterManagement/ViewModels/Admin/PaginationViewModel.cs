namespace EduCenterManagement.ViewModels.Admin;

public sealed class PaginationViewModel
{
    public const int PageSize = 10;

    public PaginationViewModel(int totalCount, int requestedPage)
    {
        TotalCount = totalCount;
        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        Page = Math.Clamp(requestedPage, 1, TotalPages);
    }

    public int TotalCount { get; }
    public int TotalPages { get; }
    public int Page { get; }
    public int Offset => (Page - 1) * PageSize;
    public int FirstRow => TotalCount == 0 ? 0 : Offset + 1;
    public int LastRow => Offset + Math.Min(PageSize, TotalCount - Offset);
}
