namespace Dawning.AgentOS.Application.Common;

public sealed record PagedRequest(int Page, int PageSize)
{
  private const int DefaultPage = 1;
  private const int DefaultPageSize = 20;
  private const int MaxPageSize = 100;

  public int NormalizedPage => Page < DefaultPage ? DefaultPage : Page;

  public int NormalizedPageSize =>
    PageSize switch
    {
      < 1 => DefaultPageSize,
      > MaxPageSize => MaxPageSize,
      _ => PageSize,
    };
}
