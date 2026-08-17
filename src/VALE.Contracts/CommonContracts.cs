namespace VALE.Contracts;

public enum TicketStatus
{
    Received,
    Parked,
    Requested,
    Delivered,
    Cancelled
}

public enum PaymentMethod
{
    Cash,
    Card,
    Transfer
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

